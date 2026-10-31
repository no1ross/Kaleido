using Kaleido.Processor.Context;
using Kaleido.Processor.Observability;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Processor.Recovery.UnitTests;

public sealed class ProcessorRecoveryServiceTests
    : SutFixture
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);
    private readonly Mock<IProcessorRecoveryStore> _store = new();
    private readonly Mock<IProcessorTransition> _transition = new();
    private readonly Mock<IProcessorRecoveryObservation> _observation = new();
    private readonly Mock<IProcessorObservability> _observability = new();
    private readonly Mock<IServiceScopeFactory> _scopes = new();

    private static readonly KaleidoOutboxEvent StepEvent = new() { EventId = Guid.NewGuid(), EventType = "kaleido.process.step_completed", Payload = "{}", OccurredUtc = DateTimeOffset.UnixEpoch };

    public ProcessorRecoveryServiceTests()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IProcessorTransition))).Returns(_transition.Object);
        provider.Setup(x => x.GetService(typeof(IProcessorObservability))).Returns(_observability.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
        _scopes.Setup(x => x.CreateScope()).Returns(scope.Object);
        _observability.Setup(x => x.BeginRecovery(It.IsAny<ProcessorRecoveryObservationDetails>())).Returns(_observation.Object);
    }

    private ProcessorRecoveryService CreateSut() => new(_scopes.Object, _store.Object, _time);

    private static PendingOutcome Outcome(int attempts = 0, DateTimeOffset? recorded = null) =>
        new()
        {
            Context = new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "intake", Version = 4 },
            StepName = "Close",
            TransitionEvent = StepEvent,
            RecordedUtc = recorded ?? Start,
            NextAttemptUtc = Start,
            Attempts = attempts,
        };

    private void Due(params PendingOutcome[] outcomes) =>
        _store.Setup(x => x.Due(It.IsAny<DateTimeOffset>())).Returns(outcomes);

    // The save is re-applied exactly as it was attempted (base version, the step's event), uncancellable.
    [Fact]
    public async Task RecoverDueAsync_SavesTheAttemptedOutcome_AndForgetsIt()
    {
        var outcome = Outcome(attempts: 2);
        Due(outcome);

        Assert.Equal(1, await CreateSut().RecoverDueAsync(TestContext.Current.CancellationToken));

        _transition.Verify(x => x.SaveAsync(outcome.Context, StepEvent, CancellationToken.None), Times.Once);
        _store.Verify(x => x.Remove(outcome.Context.ProcessId), Times.Once);
        _observation.Verify(x => x.Recovered(), Times.Once);
        _observability.Verify(x => x.BeginRecovery(new ProcessorRecoveryObservationDetails(outcome.Context.ProcessId, "Close", 3)), Times.Once);
    }

    // The process moved on meanwhile: never applied blindly, given up for an operator.
    [Fact]
    public async Task RecoverDueAsync_AConflict_ParksTheOutcome()
    {
        var outcome = Outcome();
        Due(outcome);
        _transition.Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<KaleidoOutboxEvent?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Kaleido.Exceptions.KaleidoConcurrencyException(outcome.Context.ProcessId));

        await CreateSut().RecoverDueAsync(TestContext.Current.CancellationToken);

        _store.Verify(x => x.Remove(outcome.Context.ProcessId), Times.Once);
        _observation.Verify(x => x.Parked("conflict", It.IsAny<Kaleido.Exceptions.KaleidoConcurrencyException>(), outcome), Times.Once);
        _observation.Verify(x => x.Recovered(), Times.Never);
    }

    // A store still down (a timeout included: no cancellation reaches an attempt, #517) is retried later.
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task RecoverDueAsync_AFailure_IsRetriedWithBackoff(Type failure)
    {
        var outcome = Outcome(attempts: 2);
        Due(outcome);
        _time.Advance(TimeSpan.FromMinutes(5));
        _transition.Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<KaleidoOutboxEvent?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync((Exception)Activator.CreateInstance(failure)!);

        await CreateSut().RecoverDueAsync(TestContext.Current.CancellationToken);

        _store.Verify(x => x.Reschedule(It.Is<PendingOutcome>(x => x.Attempts == 3 && x.NextAttemptUtc == _time.GetUtcNow() + TimeSpan.FromSeconds(4))), Times.Once);
        _store.Verify(x => x.Remove(It.IsAny<Guid>()), Times.Never);
        _observation.Verify(x => x.Retrying(It.IsAny<Exception>()), Times.Once);
    }

    // The recovery period: one tick before it ends, retried; at its end, given up.
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    public async Task RecoverDueAsync_AFailureAtTheEndOfTheRecoveryPeriod_ParksTheOutcome(long ticksFromTheEnd, bool parked)
    {
        var outcome = Outcome();
        Due(outcome);
        _time.Advance(ProcessorRecoveryService.MaxAge + TimeSpan.FromTicks(ticksFromTheEnd));
        _transition.Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<KaleidoOutboxEvent?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());

        await CreateSut().RecoverDueAsync(TestContext.Current.CancellationToken);

        _observation.Verify(x => x.Parked("expired", It.IsAny<TimeoutException>(), outcome), parked ? Times.Once() : Times.Never());
        _store.Verify(x => x.Reschedule(It.IsAny<PendingOutcome>()), parked ? Times.Never() : Times.Once());
    }

    [Fact]
    public async Task RecoverDueAsync_WhenTheHostStops_AttemptsNoFurtherOutcome()
    {
        Due(Outcome(), Outcome());

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().RecoverDueAsync(new CancellationToken(canceled: true)));

        _transition.Verify(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<KaleidoOutboxEvent?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(6, 32)]
    [InlineData(7, 60)]
    [InlineData(100, 60)]
    public void RetryDelay_DoublesUpToTheMaximum(int attempts, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), ProcessorRecoveryService.RetryDelay(attempts));
    }

    // Runs while the host does: each poll attempts what is due, and stopping ends it cleanly.
    [Fact]
    public async Task TheService_PollsWhileTheHostRuns_AndStopsCleanly()
    {
        var polls = 0;
        var firstPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.Setup(x => x.Due(It.IsAny<DateTimeOffset>()))
            .Callback(() => (Interlocked.Increment(ref polls) == 1 ? firstPoll : secondPoll).TrySetResult())
            .Returns([]);
        var sut = CreateSut();

        await sut.StartAsync(TestContext.Current.CancellationToken);
        await firstPoll.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.False(secondPoll.Task.IsCompleted);
        _time.Advance(ProcessorRecoveryService.PollInterval);
        await secondPoll.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(sut.ExecuteTask?.IsCompletedSuccessfully);
    }
}

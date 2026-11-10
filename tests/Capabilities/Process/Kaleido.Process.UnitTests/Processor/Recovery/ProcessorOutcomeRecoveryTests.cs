using Kaleido.Processor.Context;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Processor.Recovery.UnitTests;

public sealed class ProcessorOutcomeRecoveryTests
    : SutFixture
{
    private readonly Mock<IProcessorRecoveryStore> _store = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));

    private static readonly ProcessorContext Attempted = new() { ProcessId = Guid.NewGuid(), ProcessorName = "intake", Version = 2 };

    private static readonly KaleidoOutboxEvent StepEvent = new() { EventId = Guid.NewGuid(), EventType = "kaleido.process.step_completed", Payload = "{}", OccurredUtc = DateTimeOffset.UnixEpoch };

    private ProcessorOutcomeRecovery CreateSut() => new(_store.Object, _time);

    [Fact]
    public void Keep_AStoreFailure_KeepsTheAttemptedSaveForRecovery()
    {
        PendingOutcome? kept = null;
        _store.Setup(x => x.TryRecord(It.IsAny<PendingOutcome>())).Callback<PendingOutcome>(x => kept = x).Returns(true);

        var outcome = CreateSut().Keep(Attempted, StepEvent, "Close", new TimeoutException("store down"));

        Assert.Equal(UnsavedOutcome.PendingRecovery, outcome);
        Assert.NotNull(kept);
        Assert.Same(Attempted, kept.Context);
        Assert.Same(StepEvent, kept.TransitionEvent);
        Assert.Equal("Close", kept.StepName);
        Assert.Equal(_time.GetUtcNow(), kept.RecordedUtc);
        Assert.Equal(_time.GetUtcNow(), kept.NextAttemptUtc);
        Assert.Equal(0, kept.Attempts);
    }

    [Fact]
    public void Keep_WhenTheRecoveryStoreCannotKeepIt_IsNotSaved()
    {
        _store.Setup(x => x.TryRecord(It.IsAny<PendingOutcome>())).Returns(false);

        Assert.Equal(UnsavedOutcome.NotSaved, CreateSut().Keep(Attempted, StepEvent, "Close", new TimeoutException()));
    }

    // The process moved on since the handler saw it: the outcome can never be applied, so it is not kept.
    [Fact]
    public void Keep_AConflict_IsNeverKept()
    {
        var outcome = CreateSut().Keep(Attempted, StepEvent, "Close", new Kaleido.Exceptions.KaleidoConcurrencyException(Attempted.ProcessId));

        Assert.Equal(UnsavedOutcome.Conflict, outcome);
        _store.Verify(x => x.TryRecord(It.IsAny<PendingOutcome>()), Times.Never);
    }

    [Fact]
    public void Keep_WithoutTheAttemptOrTheFailure_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().Keep(null!, StepEvent, "Close", new TimeoutException()));
        Assert.Throws<ArgumentNullException>(() => CreateSut().Keep(Attempted, StepEvent, "Close", null!));
    }
}

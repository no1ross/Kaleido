using Kaleido.Processor.Eventing;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventOutboxTests
    : SutFixture
{
    private static KaleidoEventOutbox CreateSut(IKaleidoContextStore store, IEventPublisher publisher, IKaleidoEventSignal signal) =>
        new(store, publisher, signal, NullLogger<KaleidoEventOutbox>.Instance);

    private static KaleidoEventEnvelope<ProcessCreated, ProcessEventContext> Envelope() =>
        new()
        {
            EventType = KaleidoEventTypes.ProcessCreated,
            Context = new ProcessEventContext { RequestId = "req-1", ServiceName = "svc", ProcessId = Guid.NewGuid(), StepName = "step-a" },
            Event = new ProcessCreated { OccurredOn = DateTimeOffset.UtcNow, State = ProcessExecutionState.Active, CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow, SubmittedStepCount = 1 },
        };

    [Fact]
    public void Prepare_WithAPublisher_ReturnsTheOutboxEntry()
    {
        var envelope = Envelope();

        var entry = CreateSut(Mock.Of<IKaleidoContextStore>(), Mock.Of<IEventPublisher>(), Mock.Of<IKaleidoEventSignal>()).Prepare(envelope);

        Assert.NotNull(entry);
        Assert.Equal(envelope.Event.EventId, entry.EventId);
    }

    [Fact]
    public void Prepare_WithoutAPublisher_ReturnsNothing()
    {
        var entry = CreateSut(Mock.Of<IKaleidoContextStore>(), Mock.Of<INoOpEventPublisher>(), Mock.Of<IKaleidoEventSignal>()).Prepare(Envelope());

        Assert.Null(entry);
    }

    [Fact]
    public async Task EnqueueAsync_WritesTheEventAndWakesTheRelay()
    {
        var envelope = Envelope();
        var store = new Mock<IKaleidoContextStore>();
        var signal = new Mock<IKaleidoEventSignal>();

        await CreateSut(store.Object, Mock.Of<IEventPublisher>(), signal.Object).EnqueueAsync(envelope, CancellationToken.None);

        store.Verify(x => x.EnqueueEventsAsync(
            It.Is<IReadOnlyCollection<KaleidoOutboxEvent>>(events => events.Single().EventId == envelope.Event.EventId),
            CancellationToken.None));
        signal.Verify(x => x.Notify(), Times.Once);
    }

    [Fact]
    public async Task EnqueueAsync_WithoutAPublisher_WritesNothing()
    {
        var store = new Mock<IKaleidoContextStore>();
        var signal = new Mock<IKaleidoEventSignal>();

        await CreateSut(store.Object, Mock.Of<INoOpEventPublisher>(), signal.Object).EnqueueAsync(Envelope(), CancellationToken.None);

        store.VerifyNoOtherCalls();
        signal.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnqueueAsync_WhenTheStoreFails_NeverFailsTheCaller()
    {
        var store = new Mock<IKaleidoContextStore>();
        store
            .Setup(x => x.EnqueueEventsAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));
        var signal = new Mock<IKaleidoEventSignal>();

        await CreateSut(store.Object, Mock.Of<IEventPublisher>(), signal.Object).EnqueueAsync(Envelope(), CancellationToken.None);

        signal.Verify(x => x.Notify(), Times.Never);
    }

    [Fact]
    public void Saved_WakesTheRelay()
    {
        var signal = new Mock<IKaleidoEventSignal>();

        CreateSut(Mock.Of<IKaleidoContextStore>(), Mock.Of<IEventPublisher>(), signal.Object).Saved();

        signal.Verify(x => x.Notify(), Times.Once);
    }
}

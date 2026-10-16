using Kaleido.Processor.Eventing;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventOutboxTests
    : SutFixture
{
    private static KaleidoEventOutbox CreateSut(
        IKaleidoContextStore store,
        IEventPublisher publisher,
        IKaleidoEventSignal signal,
        IKaleidoEventPayloadWriter? payloadWriter = null,
        Microsoft.Extensions.Logging.ILogger<KaleidoEventOutbox>? logger = null) =>
        new(store, publisher, payloadWriter ?? PlainWriter().Object, signal, logger ?? NullLogger<KaleidoEventOutbox>.Instance);

    // Writes the plain (unredacted) form; redaction is the writer's own concern.
    private static Mock<IKaleidoEventPayloadWriter> PlainWriter()
    {
        var writer = new Mock<IKaleidoEventPayloadWriter>();
        writer
            .Setup(x => x.Write(It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>()))
            .Returns<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(envelope => envelope.ToOutboxEvent());

        return writer;
    }

    [Fact]
    public async Task PrepareAndEnqueue_WriteWhatThePayloadWriterProduces()
    {
        var written = new KaleidoOutboxEvent { EventId = Guid.NewGuid(), EventType = KaleidoEventTypes.ProcessCreated, Payload = "{\"redacted\":true}", OccurredUtc = DateTimeOffset.UtcNow };
        var writer = new Mock<IKaleidoEventPayloadWriter>();
        writer.Setup(x => x.Write(It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>())).Returns(written);
        var store = new Mock<IKaleidoContextStore>();
        var sut = CreateSut(store.Object, Mock.Of<IEventPublisher>(), Mock.Of<IKaleidoEventSignal>(), writer.Object);

        var prepared = sut.Prepare(Envelope());
        await sut.EnqueueAsync(Envelope(), CancellationToken.None);

        Assert.Same(written, prepared);
        store.Verify(x => x.EnqueueEventsAsync(It.Is<IReadOnlyCollection<KaleidoOutboxEvent>>(events => events.Single() == written), CancellationToken.None));
    }

    [Fact]
    public async Task WithoutAPublisher_TheWriterIsNeverCalled()
    {
        var writer = new Mock<IKaleidoEventPayloadWriter>();
        var sut = CreateSut(Mock.Of<IKaleidoContextStore>(), Mock.Of<INoOpEventPublisher>(), Mock.Of<IKaleidoEventSignal>(), writer.Object);

        sut.Prepare(Envelope());
        await sut.EnqueueAsync(Envelope(), CancellationToken.None);

        writer.VerifyNoOtherCalls();
    }

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
    public async Task EnqueueAsync_WhenTheStoreFails_LogsAnErrorWithTheEvent()
    {
        var failure = new InvalidOperationException("database down");
        var store = new Mock<IKaleidoContextStore>();
        store
            .Setup(x => x.EnqueueEventsAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<KaleidoEventOutbox>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<Microsoft.Extensions.Logging.LogLevel>())).Returns(true);
        var envelope = Envelope();

        await CreateSut(store.Object, Mock.Of<IEventPublisher>(), Mock.Of<IKaleidoEventSignal>(), logger: logger.Object)
            .EnqueueAsync(envelope, CancellationToken.None);

        var logged = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(Microsoft.Extensions.Logging.ILogger.Log));
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, logged.Arguments[0]);
        Assert.Same(failure, logged.Arguments[3]);
        Assert.Contains(envelope.Event.EventId.ToString(), logged.Arguments[2].ToString());
    }

    [Fact]
    public void Saved_WakesTheRelay()
    {
        var signal = new Mock<IKaleidoEventSignal>();

        CreateSut(Mock.Of<IKaleidoContextStore>(), Mock.Of<IEventPublisher>(), signal.Object).Saved();

        signal.Verify(x => x.Notify(), Times.Once);
    }
}

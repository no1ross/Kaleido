using Kaleido.Processor.Eventing;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventWriterTests
    : SutFixture
{
    private static KaleidoEventWriter CreateSut(
        IKaleidoOutbox outbox,
        IEventPublisher publisher,
        IKaleidoEventPayloadWriter? payloadWriter = null,
        ILogger<KaleidoEventWriter>? logger = null) =>
        new(outbox, publisher, payloadWriter ?? PlainWriter().Object, logger ?? NullLogger<KaleidoEventWriter>.Instance);

    // Writes the plain (unredacted) form; redaction is the payload writer's own concern.
    private static Mock<IKaleidoEventPayloadWriter> PlainWriter()
    {
        var writer = new Mock<IKaleidoEventPayloadWriter>();
        writer
            .Setup(x => x.Write(It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>()))
            .Returns<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(envelope => envelope.ToOutboxEvent());

        return writer;
    }

    // Kaleido's built-in outbox (or direct publication): delivers through the publisher.
    private static Mock<IKaleidoPublisherOutbox> BuiltInOutbox() => Outbox<IKaleidoPublisherOutbox>();

    private static Mock<TOutbox> Outbox<TOutbox>()
        where TOutbox : class, IKaleidoOutbox
    {
        var outbox = new Mock<TOutbox>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        outbox.Setup(x => x.CommittedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return outbox;
    }

    private static KaleidoEventEnvelope<ProcessCreated, ProcessEventContext> Envelope() =>
        new()
        {
            EventType = KaleidoEventTypes.ProcessCreated,
            Context = new ProcessEventContext { RequestId = "req-1", ServiceName = "svc", ProcessId = Guid.NewGuid(), StepName = "step-a" },
            Event = new ProcessCreated { OccurredOn = DateTimeOffset.UtcNow, State = ProcessExecutionState.Active, CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow, SubmittedStepCount = 1 },
        };

    [Fact]
    public async Task PrepareAndWrite_UseWhatThePayloadWriterProduces()
    {
        var written = new KaleidoOutboxEvent { EventId = Guid.NewGuid(), EventType = KaleidoEventTypes.ProcessCreated, Payload = "{\"redacted\":true}", OccurredUtc = DateTimeOffset.UtcNow };
        var writer = new Mock<IKaleidoEventPayloadWriter>();
        writer.Setup(x => x.Write(It.IsAny<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>())).Returns(written);
        var outbox = BuiltInOutbox();
        var sut = CreateSut(outbox.Object, Mock.Of<IEventPublisher>(), writer.Object);

        var prepared = sut.Prepare(Envelope());
        await sut.WriteAsync(Envelope(), CancellationToken.None);

        Assert.Same(written, prepared);
        outbox.Verify(x => x.AddAsync(It.Is<IReadOnlyCollection<KaleidoOutboxEvent>>(events => events.Single() == written), CancellationToken.None));
    }

    [Fact]
    public void Prepare_WithAPublisher_ReturnsTheOutboxEntry()
    {
        var envelope = Envelope();

        var entry = CreateSut(BuiltInOutbox().Object, Mock.Of<IEventPublisher>()).Prepare(envelope);

        Assert.NotNull(entry);
        Assert.Equal(envelope.Event.EventId, entry.EventId);
    }

    // Nothing to deliver to: the built-in outbox and direct publication need a publisher.
    [Fact]
    public async Task WithTheBuiltInOutbox_AndNoPublisher_NoEventIsProduced()
    {
        var writer = new Mock<IKaleidoEventPayloadWriter>();
        var outbox = BuiltInOutbox();
        var sut = CreateSut(outbox.Object, Mock.Of<INoOpEventPublisher>(), writer.Object);

        Assert.Null(sut.Prepare(Envelope()));
        await sut.WriteAsync(Envelope(), CancellationToken.None);

        writer.VerifyNoOtherCalls();
        outbox.Verify(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The host's own outbox delivers itself: events are produced without a publisher.
    [Fact]
    public async Task WithTheHostsOwnOutbox_EventsAreProducedWithoutAPublisher()
    {
        var outbox = Outbox<IKaleidoOutbox>();
        var sut = CreateSut(outbox.Object, Mock.Of<INoOpEventPublisher>());

        Assert.NotNull(sut.Prepare(Envelope()));
        await sut.WriteAsync(Envelope(), CancellationToken.None);

        outbox.Verify(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task WriteAsync_AddsTheEvent_ThenLetsTheOutboxDeliver()
    {
        var envelope = Envelope();
        var outbox = BuiltInOutbox();
        var calls = new List<string>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Callback(() => calls.Add("add")).Returns(Task.CompletedTask);
        outbox.Setup(x => x.CommittedAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("committed")).Returns(Task.CompletedTask);

        await CreateSut(outbox.Object, Mock.Of<IEventPublisher>()).WriteAsync(envelope, CancellationToken.None);

        Assert.Equal(["add", "committed"], calls);
        outbox.Verify(x => x.AddAsync(It.Is<IReadOnlyCollection<KaleidoOutboxEvent>>(events => events.Single().EventId == envelope.Event.EventId), CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_WhenTheOutboxFails_LogsAnErrorWithTheEvent_AndNeverFailsTheCaller()
    {
        var failure = new InvalidOperationException("database down");
        var outbox = BuiltInOutbox();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var logger = new Mock<ILogger<KaleidoEventWriter>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var envelope = Envelope();

        await CreateSut(outbox.Object, Mock.Of<IEventPublisher>(), logger: logger.Object).WriteAsync(envelope, CancellationToken.None);

        var logged = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Error, logged.Arguments[0]);
        Assert.Same(failure, logged.Arguments[3]);
        Assert.Contains(envelope.Event.EventId.ToString(), logged.Arguments[2].ToString(), StringComparison.Ordinal);
        outbox.Verify(x => x.CommittedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // The state is committed already: a failing delivery hook is logged, never thrown.
    [Fact]
    public async Task CommittedAsync_WhenTheOutboxFails_LogsAnError_AndNeverFailsTheCaller()
    {
        var outbox = BuiltInOutbox();
        outbox.Setup(x => x.CommittedAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("dispatcher down"));
        var logger = new Mock<ILogger<KaleidoEventWriter>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        await CreateSut(outbox.Object, Mock.Of<IEventPublisher>(), logger: logger.Object).CommittedAsync(CancellationToken.None);

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log)).Arguments[0]);
    }
}

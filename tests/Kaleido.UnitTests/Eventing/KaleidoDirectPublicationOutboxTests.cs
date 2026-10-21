using Kaleido.Processor.Eventing;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoDirectPublicationOutboxTests
    : SutFixture
{
    private static KaleidoDirectPublicationOutbox CreateSut(IEventPublisher publisher, ILogger<KaleidoDirectPublicationOutbox>? logger = null) =>
        new(publisher, logger ?? NullLogger<KaleidoDirectPublicationOutbox>.Instance);

    private static KaleidoOutboxEvent Created() =>
        new KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>
        {
            EventType = KaleidoEventTypes.ProcessCreated,
            Context = new ProcessEventContext { RequestId = "req-1", ServiceName = "svc", ProcessId = Guid.NewGuid(), StepName = "step-a" },
            Event = new ProcessCreated { OccurredOn = DateTimeOffset.UtcNow, State = ProcessExecutionState.Active, CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow, SubmittedStepCount = 1 },
        }.ToOutboxEvent();

    private static void VerifyPublished(Mock<IEventPublisher> publisher, Guid eventId, Times times) =>
        publisher.Verify(
            x => x.PublishAsync(It.Is<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(e => e.Event.EventId == eventId), It.IsAny<CancellationToken>()),
            times);

    // Never before the state it describes is committed.
    [Fact]
    public async Task AddAsync_PublishesNothingYet()
    {
        var publisher = new Mock<IEventPublisher>();

        await CreateSut(publisher.Object).AddAsync([Created()], TestContext.Current.CancellationToken);

        publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CommittedAsync_PublishesEachAddedEventOnce()
    {
        var publisher = new Mock<IEventPublisher>();
        var sut = CreateSut(publisher.Object);
        var first = Created();
        var second = Created();

        await sut.AddAsync([first, second], TestContext.Current.CancellationToken);
        await sut.CommittedAsync(TestContext.Current.CancellationToken);
        await sut.CommittedAsync(TestContext.Current.CancellationToken);

        VerifyPublished(publisher, first.EventId, Times.Once());
        VerifyPublished(publisher, second.EventId, Times.Once());
    }

    // Weaker by choice: a failed publish loses that event (logged), not the request or the others.
    [Fact]
    public async Task CommittedAsync_WhenPublishingFails_LogsTheLostEvent_AndPublishesTheRest()
    {
        var failing = Created();
        var next = Created();
        var publisher = new Mock<IEventPublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.Is<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(e => e.Event.EventId == failing.EventId), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));
        var logger = new Mock<ILogger<KaleidoDirectPublicationOutbox>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var sut = CreateSut(publisher.Object, logger.Object);

        await sut.AddAsync([failing, next], TestContext.Current.CancellationToken);
        await sut.CommittedAsync(TestContext.Current.CancellationToken);

        VerifyPublished(publisher, next.EventId, Times.Once());
        var logged = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Error, logged.Arguments[0]);
        Assert.Contains(failing.EventId.ToString(), logged.Arguments[2].ToString(), StringComparison.Ordinal);
    }
}

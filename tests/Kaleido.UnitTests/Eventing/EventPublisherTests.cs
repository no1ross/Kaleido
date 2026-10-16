using Kaleido.Eventing;
using Kaleido.Processor.Eventing;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Eventing.UnitTests;

public sealed class EventPublisherTests
    : SutFixture
{
    private static EventPublisher CreateSut(ILogger<EventPublisher>? logger = null) =>
        new(logger ?? NullLogger<EventPublisher>.Instance);

    [Fact]
    public async Task PublishAsync_CompletesWithoutThrowing()
    {
        var sut = CreateSut();
        var envelope = new KaleidoEventEnvelope<ProcessCreated, Kaleido.Eventing.ProcessEventContext>
        {
            EventType = "process.created.v1",
            Context = new Kaleido.Eventing.ProcessEventContext
            {
                RequestId = "req-1",
                ServiceName = "svc",
                ProcessId = Guid.NewGuid(),
                StepName = "step-a"
            },
            Event = new ProcessCreated
            {
                OccurredOn = DateTimeOffset.UtcNow,
                State = ProcessExecutionState.Active,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow,
                SubmittedStepCount = 0
            }
        };

        await sut.PublishAsync(envelope);
    }

    [Fact]
    public void Constructor_WarnsThatEventsAreDiscarded()
    {
        var logger = new Mock<ILogger<EventPublisher>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _ = CreateSut(logger.Object);

        var logged = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Warning, logged.Arguments[0]);
        Assert.StartsWith("No IEventPublisher has been registered.", logged.Arguments[2].ToString(), StringComparison.Ordinal);
    }
}

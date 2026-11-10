using Kaleido.Eventing;
using Kaleido.UnitTests;

namespace Kaleido.Processor.Eventing.UnitTests;

public sealed class ProcessOutboxEventDispatcherTests : SutFixture
{
    private static ProcessOutboxEventDispatcher CreateSut() => new();

    [Theory]
    [InlineData(ProcessEventTypes.ProcessCreated)]
    [InlineData(ProcessEventTypes.ProcessPlanBuilt)]
    [InlineData(ProcessEventTypes.ProcessStepCompleted)]
    [InlineData(ProcessEventTypes.ProcessExecutionCompleted)]
    public void CanHandle_ProcessEventType_IsTrue(string eventType) =>
        Assert.True(CreateSut().CanHandle(eventType));

    [Fact]
    public void CanHandle_QueryEventType_IsFalse() =>
        Assert.False(CreateSut().CanHandle("query.executed.v1"));

    [Fact]
    public async Task PublishAsync_RestoresProcessEvent()
    {
        var envelope = new KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>
        {
            EventType = ProcessEventTypes.ProcessCreated,
            Context = new ProcessEventContext { RequestId = "request", ServiceName = "service", ProcessId = Guid.NewGuid(), StepName = "step" },
            Event = new ProcessCreated { OccurredOn = DateTimeOffset.UtcNow, State = ProcessExecutionState.Active, CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow, SubmittedStepCount = 1 },
        };
        var publisher = new Mock<IEventPublisher>();

        await CreateSut().PublishAsync(publisher.Object, envelope.ToOutboxEvent(), CancellationToken.None);

        publisher.Verify(x => x.PublishAsync(It.Is<KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>>(e => e.Event.EventId == envelope.Event.EventId), CancellationToken.None), Times.Once);
    }
}

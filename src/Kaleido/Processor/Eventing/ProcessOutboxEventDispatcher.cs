namespace Kaleido.Processor.Eventing;

internal sealed class ProcessOutboxEventDispatcher : IOutboxEventDispatcher
{
    public bool CanHandle(string eventType) => eventType is
        KaleidoEventTypes.ProcessCreated or
        KaleidoEventTypes.ProcessPlanBuilt or
        KaleidoEventTypes.ProcessStepCompleted or
        KaleidoEventTypes.ProcessExecutionCompleted;

    public Task PublishAsync(IEventPublisher publisher, KaleidoOutboxEvent entry, CancellationToken cancellationToken) =>
        entry.EventType switch
        {
            KaleidoEventTypes.ProcessCreated => publisher.PublishOutboxEventAsync<ProcessCreated, ProcessEventContext>(entry, cancellationToken),
            KaleidoEventTypes.ProcessPlanBuilt => publisher.PublishOutboxEventAsync<PlanBuilt, ProcessEventContext>(entry, cancellationToken),
            KaleidoEventTypes.ProcessStepCompleted => publisher.PublishOutboxEventAsync<StepCompleted, ProcessEventContext>(entry, cancellationToken),
            KaleidoEventTypes.ProcessExecutionCompleted => publisher.PublishOutboxEventAsync<ExecutionCompleted, ProcessEventContext>(entry, cancellationToken),
            _ => throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Outbox event '{entry.EventId}' has the unknown event type '{entry.EventType}'."),
        };
}

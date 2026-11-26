namespace Kaleido.Process.Eventing;

internal sealed class ProcessOutboxEventDispatcher : IOutboxEventDispatcher
{
    public bool CanHandle(string eventType) => eventType is
        ProcessEventTypes.ProcessCreated or
        ProcessEventTypes.ProcessPlanBuilt or
        ProcessEventTypes.ProcessStepCompleted or
        ProcessEventTypes.ProcessExecutionCompleted;

    public Task PublishAsync(IEventPublisher publisher, KaleidoOutboxEvent entry, CancellationToken cancellationToken) =>
        entry.EventType switch
        {
            ProcessEventTypes.ProcessCreated => publisher.PublishOutboxEventAsync<ProcessCreated, ProcessEventContext>(entry, cancellationToken),
            ProcessEventTypes.ProcessPlanBuilt => publisher.PublishOutboxEventAsync<PlanBuilt, ProcessEventContext>(entry, cancellationToken),
            ProcessEventTypes.ProcessStepCompleted => publisher.PublishOutboxEventAsync<StepCompleted, ProcessEventContext>(entry, cancellationToken),
            ProcessEventTypes.ProcessExecutionCompleted => publisher.PublishOutboxEventAsync<ExecutionCompleted, ProcessEventContext>(entry, cancellationToken),
            _ => throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Outbox event '{entry.EventId}' has the unknown event type '{entry.EventType}'."),
        };
}

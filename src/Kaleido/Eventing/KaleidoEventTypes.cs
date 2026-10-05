namespace Kaleido.Eventing;

/// <summary>
/// Stable <see cref="KaleidoEventEnvelope{TEvent, TContext}.EventType"/> discriminators.
/// Use these in <see cref="IEventPublisher"/> implementations to route and serialize events.
/// The version suffix changes only when an event's shape changes incompatibly.
/// </summary>
public static class KaleidoEventTypes
{
    public const string ProcessCreated = "process.created.v1";

    public const string ProcessPlanBuilt = "process.plan-built.v1";

    public const string ProcessStepCompleted = "process.step-completed.v1";

    public const string ProcessExecutionCompleted = "process.execution-completed.v1";

    public const string QueryExecuted = "query.executed.v1";
}

namespace Kaleido.Eventing;

/// <summary>
/// Stable <see cref="KaleidoEventEnvelope{TEvent, TContext}.EventType"/> discriminators.
/// Use these in <see cref="IEventPublisher"/> implementations to route and serialize events.
/// The version suffix changes only when an event's shape changes incompatibly.
/// </summary>
public static class KaleidoEventTypes
{
    /// <summary>
    /// The event type of a <see cref="Kaleido.Processor.Eventing.ProcessCreated"/> event: a new process
    /// instance was created.
    /// </summary>
    public const string ProcessCreated = "process.created.v1";

    /// <summary>
    /// The event type of a <see cref="Kaleido.Processor.Eventing.PlanBuilt"/> event: the execution plan
    /// for a process request was built.
    /// </summary>
    public const string ProcessPlanBuilt = "process.plan-built.v1";

    /// <summary>
    /// The event type of a <see cref="Kaleido.Processor.Eventing.StepCompleted"/> event: one step of a
    /// process request finished.
    /// </summary>
    public const string ProcessStepCompleted = "process.step-completed.v1";

    /// <summary>
    /// The event type of an <see cref="Kaleido.Processor.Eventing.ExecutionCompleted"/> event: a process
    /// request finished executing.
    /// </summary>
    public const string ProcessExecutionCompleted = "process.execution-completed.v1";

    /// <summary>
    /// The event type of a <see cref="Kaleido.Queryable.Eventing.QueryExecuted"/> event: a Queryable
    /// source or view query was executed.
    /// </summary>
    public const string QueryExecuted = "query.executed.v1";
}

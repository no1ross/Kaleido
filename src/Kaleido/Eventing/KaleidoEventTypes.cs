namespace Kaleido.Eventing;

internal static class KaleidoEventTypes
{
    public const string ProcessCreated = "process.created.v1";
    public const string PlanBuilt = "process.plan-built.v1";
    public const string StepCompleted = "process.step-completed.v1";
    public const string ExecutionCompleted = "process.execution-completed.v1";
    public const string QueryExecuted = "query.executed.v1";
}

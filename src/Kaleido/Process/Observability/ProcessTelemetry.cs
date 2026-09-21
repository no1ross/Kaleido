namespace Kaleido.Process.Observability;

public static class ProcessTelemetry
{
    public const string ActivitySourceName =
        "Kaleido.Process";

    public const string MeterName =
        "Kaleido.Process";

    public const string ExecutionsCounterName =
        "kaleido.process.executions";

    public const string ExecutionFailuresCounterName =
        "kaleido.process.execution_failures";

    public const string ContextsInitializedCounterName =
        "kaleido.process.contexts_initialized";

    public const string ContextsLoadedCounterName =
        "kaleido.process.contexts_loaded";

    public const string SubmittedStepCountHistogramName =
        "kaleido.process.submitted_step_count";

    public const string PlanCandidateCountHistogramName =
        "kaleido.process.plan_candidate_count";

    public const string PlanExecutableCountHistogramName =
        "kaleido.process.plan_executable_count";

    public const string StepExecutionsCounterName =
        "kaleido.process.step_executions";

    public const string StepCancellationsCounterName =
        "kaleido.process.step_cancellations";

    public const string StepFailuresCounterName =
        "kaleido.process.step_failures";

    public const string HandlerExecutionsCounterName =
        "kaleido.process.handler_executions";

    public const string HandlerFailuresCounterName =
        "kaleido.process.handler_failures";
}

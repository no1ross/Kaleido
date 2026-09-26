namespace Kaleido.Process.Observability;

public static class ProcessTelemetry
{
    // ── ActivitySource / Meter ────────────────────────────────────────────────

    public const string ActivitySourceName =
        "Kaleido.Process";

    public const string MeterName =
        "Kaleido.Process";

    // ── Activity names ────────────────────────────────────────────────────────

    public const string ExecuteActivityName =
        "kaleido.process.execute";

    public const string StepActivityName =
        "kaleido.process.step";

    public const string StepHandlerActivityName =
        "kaleido.process.step.handler";

    // ── Activity event names ──────────────────────────────────────────────────

    public const string ContextInitializedEventName =
        "kaleido.process.context.initialized";

    public const string ContextLoadedEventName =
        "kaleido.process.context.loaded";

    public const string ExceptionEventName =
        "kaleido.process.exception";

    public const string ExecutionCompletedEventName =
        "kaleido.process.execution.completed";

    public const string StepCanceledEventName =
        "kaleido.process.step.canceled";

    public const string StepExceptionEventName =
        "kaleido.process.step.exception";

    public const string HandlerExceptionEventName =
        "kaleido.process.handler.exception";

    // ── Tag key names (activity tags) ─────────────────────────────────────────

    public const string TagProcessId =
        "kaleido.process.id";

    public const string TagStepName =
        "kaleido.process.step_name";

    public const string TagStepVersion =
        "kaleido.process.step_version";

    public const string TagSubmittedStepCount =
        "kaleido.process.submitted_step_count";

    public const string TagPlanCandidateCount =
        "kaleido.process.plan.candidate_count";

    public const string TagPlanExecutableCount =
        "kaleido.process.plan.executable_count";

    public const string TagDecisionType =
        "kaleido.process.decision_type";

    public const string TagExecutionStatus =
        "kaleido.process.execution_status";

    // ── Metric names ──────────────────────────────────────────────────────────

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

    public const string ExecutionDurationHistogramName =
        "kaleido.process.execution.duration";

    public const string StepDurationHistogramName =
        "kaleido.process.step.duration";
}

namespace Kaleido.Processor.Observability;

public static class ProcessorTelemetry
{
    // ── ActivitySource / Meter ────────────────────────────────────────────────

    public const string ActivitySourceName =
        "Kaleido.Processor";

    public const string MeterName =
        "Kaleido.Processor";

    // ── Activity names ────────────────────────────────────────────────────────

    public const string ExecuteActivityName =
        "kaleido.processor.execute";

    public const string StepActivityName =
        "kaleido.processor.step";

    public const string StepHandlerActivityName =
        "kaleido.processor.step.handler";

    // ── Activity event names ──────────────────────────────────────────────────

    public const string ContextInitializedEventName =
        "kaleido.processor.context.initialized";

    public const string ContextLoadedEventName =
        "kaleido.processor.context.loaded";

    public const string ExceptionEventName =
        "kaleido.processor.exception";

    public const string ExecutionCompletedEventName =
        "kaleido.processor.execution.completed";

    public const string StepCanceledEventName =
        "kaleido.processor.step.canceled";

    public const string StepExceptionEventName =
        "kaleido.processor.step.exception";

    public const string HandlerExceptionEventName =
        "kaleido.processor.handler.exception";

    // ── Tag key names (activity tags) ─────────────────────────────────────────

    public const string TagProcessId =
        "kaleido.processor.id";

    public const string TagStepName =
        "kaleido.processor.step_name";

    public const string TagStepVersion =
        "kaleido.processor.step_version";

    public const string TagSubmittedStepCount =
        "kaleido.processor.submitted_step_count";

    public const string TagPlanCandidateCount =
        "kaleido.processor.plan.candidate_count";

    public const string TagPlanExecutableCount =
        "kaleido.processor.plan.executable_count";

    public const string TagDecisionType =
        "kaleido.processor.decision_type";

    public const string TagExecutionStatus =
        "kaleido.processor.execution_status";

    // ── Metric names ──────────────────────────────────────────────────────────

    public const string ExecutionsCounterName =
        "kaleido.processor.executions";

    public const string ExecutionFailuresCounterName =
        "kaleido.processor.execution_failures";

    public const string ContextsInitializedCounterName =
        "kaleido.processor.contexts_initialized";

    public const string ContextsLoadedCounterName =
        "kaleido.processor.contexts_loaded";

    public const string SubmittedStepCountHistogramName =
        "kaleido.processor.submitted_step_count";

    public const string PlanCandidateCountHistogramName =
        "kaleido.processor.plan_candidate_count";

    public const string PlanExecutableCountHistogramName =
        "kaleido.processor.plan_executable_count";

    public const string StepExecutionsCounterName =
        "kaleido.processor.step_executions";

    public const string StepCancellationsCounterName =
        "kaleido.processor.step_cancellations";

    public const string StepFailuresCounterName =
        "kaleido.processor.step_failures";

    public const string HandlerExecutionsCounterName =
        "kaleido.processor.handler_executions";

    public const string HandlerFailuresCounterName =
        "kaleido.processor.handler_failures";

    public const string ExecutionDurationHistogramName =
        "kaleido.processor.execution.duration";

    public const string StepDurationHistogramName =
        "kaleido.processor.step.duration";
}

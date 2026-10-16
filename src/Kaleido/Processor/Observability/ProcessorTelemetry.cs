namespace Kaleido.Processor.Observability;

/// <summary>
/// The names of the Processor's telemetry: its <c>ActivitySource</c> and <c>Meter</c>, the activities
/// and activity events it emits, the tag keys it sets and its metric instruments.
/// </summary>
/// <remarks>
/// Constants only: the Processor runtime creates and records the instruments. Use these names to
/// subscribe to the Processor's traces and metrics (the <c>Kaleido.Observability.OpenTelemetry</c>
/// provider registers <see cref="ActivitySourceName"/> and <see cref="MeterName"/>) or to query them.
/// Every metric carries the <see cref="KaleidoTelemetryTags.ProcessorName"/> tag (the service name).
/// A cancellation is never recorded as a failure; one that happens while a step runs is counted once,
/// as a step cancellation.
/// </remarks>
public static class ProcessorTelemetry
{
    // ── ActivitySource / Meter ────────────────────────────────────────────────

    /// <summary>The name of the <c>ActivitySource</c> that emits the Processor's activities.</summary>
    public const string ActivitySourceName =
        "Kaleido.Processor";

    /// <summary>The name of the <c>Meter</c> that owns the Processor's metric instruments.</summary>
    public const string MeterName =
        "Kaleido.Processor";

    // ── Activity names ────────────────────────────────────────────────────────

    /// <summary>
    /// The activity for one execution request: loading or creating the process, planning the
    /// submitted steps and running them. Tagged with the request's correlation, the
    /// <see cref="TagProcessId"/>, <see cref="TagSubmittedStepCount"/>, plan counts and, on
    /// completion, <see cref="TagExecutionStatus"/>. Marked as an error only for an execution failure,
    /// never for a request refused to the caller (<see cref="ExecutionRejectedEventName"/>).
    /// </summary>
    public const string ExecuteActivityName =
        "kaleido.processor.execute";

    /// <summary>
    /// The activity for one step run within an execution, tagged with <see cref="TagStepName"/>,
    /// <see cref="TagStepVersion"/> and <see cref="TagStepKind"/>, and with
    /// <see cref="TagDecisionType"/> and <see cref="TagExecutionStatus"/> once the step's outcome is
    /// recorded. Marked as an error when the step fails.
    /// </summary>
    public const string StepActivityName =
        "kaleido.processor.step";

    /// <summary>
    /// The activity for one invocation of a step's handler, inside the step's activity, tagged with
    /// <see cref="TagStepName"/> and <see cref="TagStepVersion"/>. Marked as an error when the handler
    /// throws (other than a cancellation).
    /// </summary>
    public const string StepHandlerActivityName =
        "kaleido.processor.step.handler";

    // ── Activity event names ──────────────────────────────────────────────────

    /// <summary>
    /// Event on the execute activity: a new process was initialized (no process id was given, or an
    /// unknown one was handed off by another processor).
    /// </summary>
    public const string ContextInitializedEventName =
        "kaleido.processor.context.initialized";

    /// <summary>Event on the execute activity: an existing process was loaded from the context store.</summary>
    public const string ContextLoadedEventName =
        "kaleido.processor.context.loaded";

    /// <summary>
    /// Event on the execute activity: the execution returned a result. Not added when the execution
    /// throws.
    /// </summary>
    public const string ExecutionCompletedEventName =
        "kaleido.processor.execution.completed";

    // The caller's request was refused (validation, authorization, a concurrent change): not a failure.
    /// <summary>
    /// Event on the execute activity: the caller's request was refused (a validation error, an
    /// authorization refusal or a concurrent change). Tagged with <see cref="TagRejectionCode"/>. Not
    /// an execution failure: the activity is not marked as an error and no failure is counted.
    /// </summary>
    public const string ExecutionRejectedEventName =
        "kaleido.processor.execution.rejected";

    // The rejection code tag value for an authorization refusal (which has no error code of its own).
    /// <summary>
    /// The <see cref="TagRejectionCode"/> value of <see cref="ExecutionRejectedEventName"/> for an
    /// authorization refusal, which has no error code of its own.
    /// </summary>
    public const string AuthorizationRejectionCode =
        "authorization";

    /// <summary>Event on the step activity: the step run was canceled.</summary>
    public const string StepCanceledEventName =
        "kaleido.processor.step.canceled";

    // ── Tag key names (activity tags) ─────────────────────────────────────────

    /// <summary>
    /// The process id (a <see cref="System.Guid"/> string). Set on the execute activity, and also on the
    /// Queryable execution activity and the HTTP request activity when the inbound correlation carries
    /// a process id.
    /// </summary>
    public const string TagProcessId =
        "kaleido.processor.id";

    /// <summary>
    /// The step's name (its type name). Set on the step and handler activities, and a tag of the
    /// step-level metrics.
    /// </summary>
    public const string TagStepName =
        "kaleido.processor.step_name";

    /// <summary>
    /// The step's declared version. Set on the step and handler activities, and a tag of the
    /// step-level metrics when the version is not empty.
    /// </summary>
    public const string TagStepVersion =
        "kaleido.processor.step_version";

    /// <summary>The number of steps submitted in the execution request, on the execute activity.</summary>
    public const string TagSubmittedStepCount =
        "kaleido.processor.submitted_step_count";

    /// <summary>The number of candidates in the execution plan, on the execute activity.</summary>
    public const string TagPlanCandidateCount =
        "kaleido.processor.plan.candidate_count";

    /// <summary>
    /// The number of plan candidates included in execution (the ones that will be run), on the
    /// execute activity.
    /// </summary>
    public const string TagPlanExecutableCount =
        "kaleido.processor.plan.executable_count";

    /// <summary>
    /// The step's decision, an <see cref="ExecutionDecisionType"/> member name (e.g. <c>Continue</c>,
    /// <c>Complete</c>, <c>Rejected</c>), on the step activity.
    /// </summary>
    public const string TagDecisionType =
        "kaleido.processor.decision_type";

    /// <summary>
    /// An execution status member name. On the step activity it is the step's
    /// <see cref="StepExecutionStatus"/>; on the execute activity it is the process's final
    /// <see cref="ProcessExecutionState"/>.
    /// </summary>
    public const string TagExecutionStatus =
        "kaleido.processor.execution_status";

    /// <summary><c>properties</c> or <c>information</c> (the step's input is an information response).</summary>
    public const string TagStepKind =
        "kaleido.processor.step_kind";

    /// <summary>
    /// The id of the information request a step presented, on the step activity (with
    /// <see cref="InformationRequestedEventName"/>).
    /// </summary>
    public const string TagInformationRequestId =
        "kaleido.processor.information_request_id";

    /// <summary>
    /// Why a request or answer was refused. On <see cref="ExecutionRejectedEventName"/> it is the
    /// refusing exception's error code, or <see cref="AuthorizationRejectionCode"/>; on
    /// <see cref="InformationResponsesRejectedCounterName"/> it is a
    /// <c>pro_information_response_*</c> error code.
    /// </summary>
    public const string TagRejectionCode =
        "kaleido.processor.rejection_code";

    // ── Metric names ──────────────────────────────────────────────────────────

    /// <summary>
    /// Counter: execution requests started. Tagged with the processor name and, when the request
    /// came from another processor, <see cref="KaleidoTelemetryTags.CallingProcessor"/>.
    /// </summary>
    public const string ExecutionsCounterName =
        "kaleido.processor.executions";

    /// <summary>
    /// Counter: executions that failed with an unexpected exception. Requests refused to the caller
    /// (validation, authorization, a concurrent change) and cancellations are not counted; neither are
    /// step failures, which the execution records as a step outcome.
    /// </summary>
    public const string ExecutionFailuresCounterName =
        "kaleido.processor.execution_failures";

    /// <summary>Counter: new processes initialized (see <see cref="ContextInitializedEventName"/>).</summary>
    public const string ContextsInitializedCounterName =
        "kaleido.processor.contexts_initialized";

    /// <summary>Counter: existing processes loaded from the context store (see <see cref="ContextLoadedEventName"/>).</summary>
    public const string ContextsLoadedCounterName =
        "kaleido.processor.contexts_loaded";

    /// <summary>
    /// Histogram (count, no unit): the number of steps submitted per execution request. Tagged like
    /// <see cref="ExecutionsCounterName"/>.
    /// </summary>
    public const string SubmittedStepCountHistogramName =
        "kaleido.processor.submitted_step_count";

    /// <summary>Histogram (count, no unit): the number of candidates in each execution plan.</summary>
    public const string PlanCandidateCountHistogramName =
        "kaleido.processor.plan_candidate_count";

    /// <summary>
    /// Histogram (count, no unit): the number of plan candidates included in execution, per
    /// execution plan.
    /// </summary>
    public const string PlanExecutableCountHistogramName =
        "kaleido.processor.plan_executable_count";

    /// <summary>
    /// Counter: step runs started, whatever their outcome. Tagged with the processor name,
    /// <see cref="TagStepName"/> and, when not empty, <see cref="TagStepVersion"/> (as are all
    /// step-level metrics).
    /// </summary>
    public const string StepExecutionsCounterName =
        "kaleido.processor.step_executions";

    /// <summary>Counter: step runs canceled (see <see cref="StepCanceledEventName"/>).</summary>
    public const string StepCancellationsCounterName =
        "kaleido.processor.step_cancellations";

    /// <summary>
    /// Counter: step runs that threw, including a <see cref="KaleidoValidationException"/>.
    /// Cancellations, concurrency conflicts and handler rejections are not counted.
    /// </summary>
    public const string StepFailuresCounterName =
        "kaleido.processor.step_failures";

    /// <summary>Counter: step handler invocations started.</summary>
    public const string HandlerExecutionsCounterName =
        "kaleido.processor.handler_executions";

    /// <summary>Counter: step handler invocations that threw an exception other than a cancellation.</summary>
    public const string HandlerFailuresCounterName =
        "kaleido.processor.handler_failures";

    /// <summary>Counter: information requests presented by steps (see <see cref="InformationRequestedEventName"/>).</summary>
    public const string InformationRequestsCounterName =
        "kaleido.processor.information_requests";

    /// <summary>
    /// Counter: answers to an information request refused at planning, one per refusal message.
    /// Tagged with the processor name, <see cref="TagStepName"/> and <see cref="TagRejectionCode"/>.
    /// </summary>
    public const string InformationResponsesRejectedCounterName =
        "kaleido.processor.information_responses_rejected";

    /// <summary>
    /// Event on the step activity: the step's outcome presented an information request (its id is
    /// <see cref="TagInformationRequestId"/>).
    /// </summary>
    public const string InformationRequestedEventName =
        "kaleido.processor.information_requested";

    /// <summary>
    /// Histogram (unit <c>s</c>, seconds): the duration of an execution request, recorded whatever its
    /// outcome.
    /// </summary>
    public const string ExecutionDurationHistogramName =
        "kaleido.processor.execution.duration";

    /// <summary>
    /// Histogram (unit <c>s</c>, seconds): the duration of a step run, recorded whatever its outcome.
    /// Tagged like <see cref="StepExecutionsCounterName"/>.
    /// </summary>
    public const string StepDurationHistogramName =
        "kaleido.processor.step.duration";
}

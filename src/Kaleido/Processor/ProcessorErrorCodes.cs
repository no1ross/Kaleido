namespace Kaleido.Processor;

/// <summary>
/// Stable Processor codes shared by startup, planning, validation, and execution.
/// Message severity is carried separately by <see cref="MessageType"/>.
/// </summary>
public static class ProcessorErrorCodes
{
    /// <summary>A step type is missing [ProcessStep].</summary>
    public const string MissingAttribute = "pro_missing_attribute";

    /// <summary>A step has no registered handler.</summary>
    public const string MissingHandler = "pro_missing_handler";

    /// <summary>A handler has an invalid signature.</summary>
    public const string InvalidHandler = "pro_invalid_handler";

    /// <summary>Registered step names are duplicated.</summary>
    public const string DuplicateStep = "pro_duplicate_step";

    /// <summary>A step registration is structurally invalid.</summary>
    public const string InvalidRegistration = "pro_invalid_registration";

    /// <summary>The requested step is not registered.</summary>
    public const string UnknownStep = "pro_unknown_step";

    /// <summary>
    /// The request names a process that does not exist, and is not a handoff from another
    /// processor: a new process is never started under an unknown id by mistake.
    /// </summary>
    public const string ProcessNotFound = "pro_process_not_found";

    /// <summary>
    /// The process was changed by another request since this one loaded it (optimistic
    /// concurrency): nothing was saved. Read the process state again and decide on the next step.
    /// HTTP 409.
    /// </summary>
    public const string ProcessConflict = "pro_process_conflict";

    /// <summary>
    /// An idempotency key was sent again with a different request: the key identifies one logical call
    /// and its stored answer belongs to the first request. Use a new key for a new call. HTTP 422.
    /// </summary>
    public const string IdempotencyKeyReused = "pro_idempotency_key_reused";

    /// <summary>The idempotency key is longer than 200 characters. HTTP 400.</summary>
    public const string InvalidIdempotencyKey = "pro_invalid_idempotency_key";

    /// <summary>
    /// The first request with this idempotency key is still running, so this one did not run. Retry
    /// with the same key after the <c>Retry-After</c> delay to get its answer. HTTP 409.
    /// </summary>
    public const string IdempotencyInProgress = "pro_idempotency_in_progress";

    /// <summary>
    /// The first request with this idempotency key did not finish with an answer: it never finished (e.g.
    /// the service stopped while it ran) or failed after its step may have run (an unexpected error, a
    /// cancellation). It may or may not have run, so it is not run again with this key until the record
    /// expires: read the process state and decide on the next step. HTTP 409.
    /// </summary>
    public const string IdempotencyOutcomeUnknown = "pro_idempotency_outcome_unknown";

    /// <summary>The supplied step request is invalid.</summary>
    public const string InvalidRequest = "pro_invalid_request";

    /// <summary>A requested property was not found.</summary>
    public const string PropertyNotFound = "pro_property_not_found";

    /// <summary>A request value could not be converted.</summary>
    public const string ConversionFailed = "pro_conversion_failed";

    /// <summary>Step validation failed without a more specific built-in code.</summary>
    public const string ValidationFailed = "pro_validation_failed";

    /// <summary>A required step field is missing.</summary>
    public const string Required = "pro_required";

    /// <summary>A step field violates a length constraint.</summary>
    public const string InvalidLength = "pro_invalid_length";

    /// <summary>A step field is outside its allowed range.</summary>
    public const string OutOfRange = "pro_out_of_range";

    /// <summary>A step field does not match its declared format.</summary>
    public const string InvalidFormat = "pro_invalid_format";

    /// <summary>A previously completed step requires no additional processing.</summary>
    public const string AlreadyProcessed = "pro_already_processed";

    /// <summary>A step violates an execution consistency rule.</summary>
    public const string ConsistencyViolation = "pro_consistency_violation";

    /// <summary>A required dependency has not been completed.</summary>
    public const string DependencyNotSatisfied = "pro_dependency_not_satisfied";

    /// <summary>A required dependency has been completed.</summary>
    public const string DependencySatisfied = "pro_dependency_satisfied";

    /// <summary>Step handler execution failed.</summary>
    public const string HandlerExecutionFailed = "pro_handler_execution_failed";

    /// <summary>An exception interrupted step processing.</summary>
    public const string ExceptionThrown = "pro_exception_thrown";

    /// <summary>A required next step is invalid.</summary>
    public const string InvalidRequiredStep = "pro_invalid_required_step";

    /// <summary>The required next step cannot be executed.</summary>
    public const string RequiredStepNotAllowed = "pro_required_step_not_allowed";

    /// <summary>
    /// A handler handed the process off to the service it runs in; a handoff names another
    /// service. Reported as a process violation: the process keeps its state.
    /// </summary>
    public const string InvalidHandOff = "pro_invalid_hand_off";

    /// <summary>Step execution was cancelled.</summary>
    public const string ExecutionCanceled = "pro_execution_canceled";

    /// <summary>An unexpected framework error interrupted a step.</summary>
    public const string FrameworkException = "pro_framework_exception";

    /// <summary>A Process message was produced.</summary>
    public const string ProcessMessage = "pro_process_message";

    /// <summary>A repeatable step remains eligible after prior execution.</summary>
    public const string RepeatableStep = "pro_repeatable_step";

    /// <summary>A process execution result contained no entry for the step that was executed.</summary>
    public const string MissingStepResult = "pro_missing_step_result";

    /// <summary>
    /// The submitted step is not a next step of the process: not the pending required step,
    /// or not available under the process rules.
    /// </summary>
    public const string StepNotAvailable = "pro_step_not_available";

    /// <summary>
    /// The process has ended here: it is complete, it ended in a business failure, or it was handed
    /// off to another service. Nobody can run a step of this service on it (a handoff back from the
    /// service that holds it excepted); start a new process, or continue on the named service (#512).
    /// </summary>
    public const string ProcessEnded = "pro_process_ended";

    /// <summary>A handler returned an information request that is not well-formed.</summary>
    public const string InformationRequestInvalid = "pro_information_request_invalid";

    /// <summary>An information step was required without an information request.</summary>
    public const string InformationRequestMissing = "pro_information_request_missing";

    /// <summary>
    /// Answers were submitted for an information request that is not pending (another step,
    /// another request, or nothing was requested).
    /// </summary>
    public const string InformationResponseMismatch = "pro_information_response_mismatch";

    /// <summary>
    /// Answers carry the exchange id of an earlier time the questions were put, not of the pending
    /// request (a delayed answer, a stale form, a replayed continuation - perhaps of the same
    /// questionnaire with the same item ids). Rejected before any handler runs; answer the current
    /// questions (#442).
    /// </summary>
    public const string InformationResponseStale = "pro_information_response_stale";

    /// <summary>A presented question has no answer.</summary>
    public const string InformationResponseUnanswered = "pro_information_response_unanswered";

    /// <summary>
    /// An answer doesn't fit its question: unknown item, wrong value for the item type, a choice
    /// that wasn't offered, or several answers to a question that doesn't repeat.
    /// </summary>
    public const string InformationResponseInvalidAnswer = "pro_information_response_invalid_answer";

    internal const string InformationResponsePrefix = "pro_information_response_";
}

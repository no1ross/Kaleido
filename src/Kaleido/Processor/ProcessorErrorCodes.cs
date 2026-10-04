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

    /// <summary>Step execution was cancelled.</summary>
    public const string ExecutionCanceled = "pro_execution_canceled";

    /// <summary>An unexpected framework error interrupted a step.</summary>
    public const string FrameworkException = "pro_framework_exception";

    /// <summary>A Process message was produced.</summary>
    public const string ProcessMessage = "pro_process_message";

    /// <summary>A repeatable step remains eligible after prior execution.</summary>
    public const string RepeatableStep = "pro_repeatable_step";
}

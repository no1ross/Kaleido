namespace Kaleido.Process;

/// <summary>
/// Standard error codes used across Process validation and execution.
/// </summary>
public static class ProcessErrorCodes
{
    /// <summary>
    /// Process step is missing required ProcessStepAttribute.
    /// </summary>
    public const string MissingStepAttribute = "MISSING_STEP_ATTRIBUTE";

    /// <summary>
    /// Process step name is empty or whitespace.
    /// </summary>
    public const string InvalidStepName = "INVALID_STEP_NAME";

    /// <summary>
    /// Process step version is empty or whitespace.
    /// </summary>
    public const string InvalidStepVersion = "INVALID_STEP_VERSION";

    /// <summary>
    /// Multiple process steps have the same name.
    /// </summary>
    public const string DuplicateStepName = "DUPLICATE_STEP_NAME";

    /// <summary>
    /// Process step has no registered handler.
    /// </summary>
    public const string MissingStepHandler = "MISSING_STEP_HANDLER";

    /// <summary>
    /// Process step has multiple registered handlers.
    /// </summary>
    public const string MultipleStepHandlers = "MULTIPLE_STEP_HANDLERS";

    /// <summary>
    /// Process step has circular dependencies.
    /// </summary>
    public const string CircularDependency = "CIRCULAR_DEPENDENCY";

    /// <summary>
    /// Process step references a dependency that does not exist.
    /// </summary>
    public const string MissingDependency = "MISSING_DEPENDENCY";

    /// <summary>
    /// Step has already been executed and is not repeatable.
    /// </summary>
    public const string StepNotRepeatable = "STEP_NOT_REPEATABLE";

    /// <summary>
    /// Step execution violates history consistency (invalid state transition).
    /// </summary>
    public const string HistoryConsistencyViolation = "HISTORY_CONSISTENCY_VIOLATION";

    /// <summary>
    /// Handler returned an invalid result.
    /// </summary>
    public const string InvalidHandlerResult = "INVALID_HANDLER_RESULT";

    /// <summary>
    /// Handler threw an exception during execution.
    /// </summary>
    public const string HandlerExecutionFailed = "HANDLER_EXECUTION_FAILED";

    /// <summary>
    /// Process context state is corrupted or invalid.
    /// </summary>
    public const string StateCorruption = "STATE_CORRUPTION";

    /// <summary>
    /// Step not found in the local registry.
    /// </summary>
    public const string StepNotFound = "STEP_NOT_FOUND";

    /// <summary>
    /// Step input deserialization failed.
    /// </summary>
    public const string StepDeserializationFailed = "STEP_DESERIALIZATION_FAILED";

    /// <summary>
    /// Type filter failed during step registration.
    /// </summary>
    public const string TypeFilterFailed = "TYPE_FILTER_FAILED";
}

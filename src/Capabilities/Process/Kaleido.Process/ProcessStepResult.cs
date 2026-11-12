namespace Kaleido.Process;

/// <summary>
/// The planning and execution outcome of one submitted step in a <see cref="ProcessResult"/>.
/// <see cref="CandidateStatus"/> describes whether planning admitted it;
/// <see cref="ExecutionStatus"/> describes what happened if its handler ran.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepResult
{
    /// <summary>The submitted step name, matched case-insensitively against the local registered
    /// step types in <see cref="ProcessorRequest.Steps"/>.</summary>
    public required string StepName
    {
        get;
        init;
    }

    /// <summary>
    /// The step's planning result: <see cref="StepCandidateStatus.Built"/> when it was valid,
    /// <see cref="StepCandidateStatus.Invalid"/> when it is not registered, could not be read, failed
    /// validation, misses a dependency, or is not allowed to run next, and
    /// <see cref="StepCandidateStatus.Satisfied"/> when it had already completed and is not repeatable.
    /// </summary>
    public required StepCandidateStatus CandidateStatus
    {
        get;
        init;
    }

    /// <summary>
    /// <see langword="true"/> when the step was put in the execution plan of this request. An
    /// included step runs only when execution reaches it: the first admitted step runs, and later
    /// ones only when the process continues into them.
    /// </summary>
    public required bool IncludedInExecutionPlan
    {
        get;
        init;
    }

    /// <summary>The handler's typed result, if it returned one. A caller may inspect it as the
    /// handler's declared response type; <see langword="null"/> when no result was produced. The
    /// runtime does not serialize or convert this value for a transport.</summary>
    public object? Response
    {
        get;
        init;
    }

    /// <summary>
    /// The step's execution status in this request; <see cref="StepExecutionStatus.Pending"/> when
    /// it did not run.
    /// </summary>
    public StepExecutionStatus ExecutionStatus
    {
        get;
        init;
    }

    /// <summary>
    /// The runtime's messages about the step: its planning and validation messages, followed by the
    /// messages from its execution when it ran (a process violation, a validation failure, an
    /// unexpected error or a cancellation).
    /// </summary>
    public IReadOnlyCollection<StepProcessingMessage> RuntimeMessages
    {
        get;
        init;
    }
        = [];

    /// <summary>
    /// The business messages the step's handler returned; empty when the step did not run or
    /// failed with an error.
    /// </summary>
    public IReadOnlyCollection<ProcessMessage> BusinessMessages
    {
        get;
        init;
    }
        = [];
}

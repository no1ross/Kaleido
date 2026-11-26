namespace Kaleido.Process;

/// <summary>
/// Where a process is: stored in its process state and returned after each request (#536).
/// </summary>
/// <remarks>
/// Set when the process is created and from the result of each step whose handler changes it. A step
/// that is rejected, fails validation, throws an unexpected error, breaks a process rule or is
/// cancelled leaves the state unchanged; only the step's <see cref="StepExecutionStatus"/> records it.
/// </remarks>
public enum ProcessExecutionState
{
    /// <summary>
    /// The process is running: the state of a new process, and the state saved after a step that
    /// continues into another step submitted in the same request.
    /// </summary>
    Active,

    /// <summary>
    /// The process has ended: the last step's handler completed it (<c>Complete(...)</c>), or it
    /// required no next step and the process rules make no further step available. No further step
    /// runs (<c>pro_process_ended</c>, ADR 0033).
    /// </summary>
    Complete,

    /// <summary>
    /// The process has ended: the last step's handler returned a business failure, recorded as
    /// <see cref="StepExecutionStatus.Failed"/>. No further step runs (<c>pro_process_ended</c>); a new
    /// process is started instead (ADR 0033).
    /// </summary>
    BusinessFailure,

    /// <summary>
    /// The last step handed the process off to another processor, named by the process's target
    /// processor name; the process continues there. This service runs none of its steps on it
    /// (<c>pro_process_ended</c>) unless that processor hands it back (ADR 0033).
    /// </summary>
    HandOff,

    /// <summary>
    /// The process waits for its required step, which the caller must submit next.
    /// </summary>
    AwaitingRequiredStep,

    /// <summary>
    /// The process waits for the caller to submit one of its available steps.
    /// </summary>
    AwaitingStepSelection,

    /// <summary>
    /// The process waits for the answers to the required step's information request.
    /// </summary>
    AwaitingInformation,
}

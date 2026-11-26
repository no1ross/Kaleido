namespace Kaleido.Process;

/// <summary>
/// What happened to a step: the one step status, used in the process history (each step's last
/// attempt), in the answer to the step that was sent, and in the step-completed event (#536).
/// </summary>
/// <remarks>
/// Where the process is afterwards is <see cref="ProcessExecutionState"/>. Only
/// <see cref="Completed"/> and <see cref="Failed"/> come from a handler's result that changes the
/// process; every other status leaves the process as it was.
/// </remarks>
public enum StepExecutionStatus
{
    /// <summary>
    /// The step has not run: in the history, it never ran; in an answer, the submitted step did not
    /// run in this request (it is not a next step, misses a dependency or was already done; the
    /// framework messages say why).
    /// </summary>
    Pending,

    /// <summary>
    /// The step ran and its handler succeeded.
    /// </summary>
    Completed,

    /// <summary>
    /// The step's input failed its validation rules (or a handler raised a validation error): it
    /// did not complete, and the process keeps its state. Correct the input and send it again.
    /// </summary>
    ValidationFailed,

    /// <summary>
    /// A defect: the handler threw an unexpected error, or its result broke a process rule (an
    /// invalid next step or information request, a handoff to the service itself). The process
    /// keeps the state it had before the step, so the step can be retried once fixed (ADR 0008).
    /// </summary>
    Exception,

    /// <summary>
    /// The caller cancelled the request while the step's handler was running: the step did not
    /// finish, and the process keeps the state it had before the step, so the step can be sent
    /// again.
    /// </summary>
    Canceled,

    /// <summary>
    /// The step's handler returned a business failure: the step did not complete, and the process
    /// is in <see cref="ProcessExecutionState.BusinessFailure"/>.
    /// </summary>
    Failed,

    /// <summary>
    /// The handler rejected the step's input as correctable: the step was not completed and the
    /// process was not changed. Correct the input and send it again, or choose another step.
    /// </summary>
    Rejected,
}

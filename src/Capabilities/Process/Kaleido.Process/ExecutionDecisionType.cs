namespace Kaleido.Process;

/// <summary>
/// What the runtime concluded from a step handler's result: the audit vocabulary of the
/// <c>StepCompleted</c> process event, which carries it as <c>DecisionType</c> (#536).
/// </summary>
/// <remarks>
/// Reporting reads it to see why the process moved where it did. The transport layers' answers do
/// not carry it: callers get the <see cref="ProcessExecutionState"/> and the step's
/// <see cref="StepExecutionStatus"/>. The decision sets the process state, and only the decision
/// does.
/// </remarks>
public enum ExecutionDecisionType
{
    /// <summary>
    /// The step completed, and the process continues at once with the next step submitted in the
    /// same request.
    /// </summary>
    Continue,

    /// <summary>
    /// The step completed, and nothing more is available: the process is complete.
    /// </summary>
    Complete,

    /// <summary>
    /// The handler returned a business failure: the step is recorded as
    /// <see cref="StepExecutionStatus.Failed"/>, and the process is in
    /// <see cref="ProcessExecutionState.BusinessFailure"/>.
    /// </summary>
    BusinessFailure,

    /// <summary>
    /// The handler's result broke a process rule (an illegal next step, an invalid information
    /// request, a handoff to the service itself): the step is recorded as
    /// <see cref="StepExecutionStatus.Exception"/>, and the process keeps its state.
    /// </summary>
    ProcessViolation,

    /// <summary>
    /// The step completed and handed the process off to another service.
    /// </summary>
    HandOff,

    /// <summary>
    /// The step completed, and the process waits for a specific next step.
    /// </summary>
    AwaitingRequiredStep,

    /// <summary>
    /// The step completed, and the process waits for the caller to choose one of its available steps.
    /// </summary>
    AwaitingStepSelection,

    /// <summary>
    /// The step completed, and the process waits for the answers to an information request.
    /// </summary>
    AwaitingInformation,

    /// <summary>
    /// The handler rejected the step's input as correctable: nothing is saved and no event is written,
    /// so <c>StepCompleted</c> never carries it.
    /// </summary>
    Rejected,
}

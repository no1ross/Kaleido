namespace Kaleido.Processor.Eventing;

/// <summary>
/// A Process runtime event: the payload of an envelope with a <see cref="ProcessEventContext"/>,
/// handed to <see cref="IEventPublisher"/> through the event outbox.
/// </summary>
/// <remarks>
/// The concrete events are <see cref="ProcessCreated"/>, <see cref="PlanBuilt"/>,
/// <see cref="StepCompleted"/> and <see cref="ExecutionCompleted"/>, published under the matching
/// <see cref="KaleidoEventTypes"/> names.
/// </remarks>
public interface IProcessEvent : IKaleidoEvent;

/// <summary>
/// The common base of the Process runtime events: a new <see cref="EventId"/> per event and the
/// time it occurred.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract record ProcessEventBase : IProcessEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc/>
    public required DateTimeOffset OccurredOn { get; init; }
}

/// <summary>
/// A new process instance was created: by a request without a process id, or by a request with an
/// unknown process id that another processor hands off to this one. Saved together with the new
/// process state, before any step of the request runs, so every created process is announced
/// (when an event publisher is registered), whether or not a step of the request runs.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessCreated : ProcessEventBase
{
    /// <summary>The new process's state; <see cref="ProcessExecutionState.Active"/> at creation.</summary>
    public required ProcessExecutionState State { get; init; }

    /// <summary>When the process was created (UTC), from <see cref="Context.ProcessorContext.CreatedUtc"/>.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }

    /// <summary>
    /// The new process's <see cref="Context.ProcessorContext.UpdatedUtc"/> (UTC), set when its state
    /// was initialized, like <see cref="CreatedUtc"/>.
    /// </summary>
    public required DateTimeOffset UpdatedUtc { get; init; }

    /// <summary>The step names submitted with the request that created the process, as submitted.</summary>
    public IReadOnlyCollection<string> SubmittedStepNames { get; init; } = [];

    /// <summary>The number of entries in <see cref="SubmittedStepNames"/>.</summary>
    public required int SubmittedStepCount { get; init; }
}

/// <summary>
/// A planning message about one candidate of a <see cref="PlanBuilt"/> event, copied from the
/// runtime's <see cref="StepProcessingMessage"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlanBuiltCandidateMessage
{
    /// <summary>The message's severity; an <see cref="MessageType.Error"/> keeps the candidate out of the execution plan.</summary>
    public required MessageType Type { get; init; }

    /// <summary>The machine-readable code, a <see cref="ProcessorErrorCodes"/> value.</summary>
    public required string Code { get; init; }

    /// <summary>The human-readable explanation.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// One submitted step as planned in a <see cref="PlanBuilt"/> event: whether it was valid and
/// whether it was put in the execution plan.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlanBuiltCandidate
{
    /// <summary>The step name as submitted.</summary>
    public required string StepName { get; init; }

    /// <summary>
    /// The candidate's planning result: <see cref="StepCandidateStatus.Built"/> when valid,
    /// <see cref="StepCandidateStatus.Invalid"/> when it was rejected (see <see cref="Messages"/>), or
    /// <see cref="StepCandidateStatus.Satisfied"/> when the step had already completed and is not repeatable.
    /// </summary>
    public required StepCandidateStatus CandidateStatus { get; init; }

    /// <summary><see langword="true"/> when the step was put in the execution plan of this request.</summary>
    public required bool IncludedInExecutionPlan { get; init; }

    /// <summary>The planning and validation messages about this candidate.</summary>
    public IReadOnlyCollection<PlanBuiltCandidateMessage> Messages { get; init; } = [];
}

/// <summary>
/// The execution plan for a request was built: every submitted step as a candidate, and the process
/// state the plan was built against (before any step of the request runs).
/// </summary>
/// <remarks>
/// Written once per request, after planning and on its own rather than with a state save, so it is
/// published even when no step of the request runs.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record PlanBuilt : ProcessEventBase
{
    /// <summary>The process state before any step of this request runs.</summary>
    public required ProcessExecutionState State { get; init; }

    /// <summary>The step the process was waiting for before this request, if any.</summary>
    public string? RequiredStep { get; init; }

    /// <summary>The information request presented for <see cref="RequiredStep"/>, if any.</summary>
    public InformationRequest? RequiredInformationRequest { get; init; }

    /// <summary>The processor the process had been handed off to before this request, if any.</summary>
    public string? TargetProcessorName { get; init; }

    /// <summary>
    /// The available steps before this request, as stored in the process state: set when the
    /// process awaits step selection, and empty for a new process (its initial steps are not stored).
    /// </summary>
    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    /// <summary>The step names submitted with the request, as submitted.</summary>
    public IReadOnlyCollection<string> SubmittedStepNames { get; init; } = [];

    /// <summary>The number of entries in <see cref="SubmittedStepNames"/>.</summary>
    public required int SubmittedStepCount { get; init; }

    /// <summary>The number of entries in <see cref="Candidates"/>: one per submitted step.</summary>
    public required int CandidateCount { get; init; }

    /// <summary>The number of candidates put in the execution plan (<see cref="PlanBuiltCandidate.IncludedInExecutionPlan"/>).</summary>
    public required int ExecutableCount { get; init; }

    /// <summary>
    /// The planned candidates: the ones that passed validation first, in dependency order, then the
    /// rest. A candidate rejected afterwards because it may not run next keeps its place, with
    /// <see cref="PlanBuiltCandidate.IncludedInExecutionPlan"/> <see langword="false"/>.
    /// </summary>
    public IReadOnlyCollection<PlanBuiltCandidate> Candidates { get; init; } = [];
}

/// <summary>
/// A step's handler ran and its result was applied to the process: the step's outcome and the
/// process state after it.
/// </summary>
/// <remarks>
/// Saved together with that process state (both or neither). Not written for a step its handler
/// rejects (nothing is saved), nor for a step that fails with a validation error, an unexpected
/// error or a cancellation; those are recorded in the process state and the request's result only.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record StepCompleted : ProcessEventBase
{
    /// <summary>The step name as submitted.</summary>
    public required string StepName { get; init; }

    /// <summary>The step's input: the step instance built from the submitted value.</summary>
    public object? Request { get; init; }

    /// <summary>The handler's response payload, or <see langword="null"/> when it returned none.</summary>
    public object? Response { get; init; }

    /// <summary>
    /// The runtime's decision about the step's result, which set <see cref="ProcessState"/>; never
    /// <see cref="ExecutionDecisionType.Rejected"/>.
    /// </summary>
    public required ExecutionDecisionType DecisionType { get; init; }

    /// <summary>
    /// What happened to the step, from <see cref="DecisionType"/>: <see cref="StepExecutionStatus.Completed"/>,
    /// <see cref="StepExecutionStatus.Failed"/> for a business failure, or
    /// <see cref="StepExecutionStatus.Exception"/> for a process violation (the process kept its state;
    /// see <see cref="RuntimeMessages"/>).
    /// </summary>
    public required StepExecutionStatus Status { get; init; }

    /// <summary>The business messages the step's handler returned.</summary>
    public IReadOnlyCollection<ProcessMessage> BusinessMessages { get; init; } = [];

    /// <summary>The runtime's messages about the decision; set for a process violation, otherwise empty.</summary>
    public IReadOnlyCollection<StepProcessingMessage> RuntimeMessages { get; init; } = [];

    /// <summary>The process state after the step.</summary>
    public required ProcessExecutionState ProcessState { get; init; }

    /// <summary>The step the process requires next after this step, if any.</summary>
    public string? RequiredStep { get; init; }

    /// <summary>The information request presented for <see cref="RequiredStep"/>, if any.</summary>
    public InformationRequest? RequiredInformationRequest { get; init; }

    /// <summary>The processor the step handed the process off to, if any.</summary>
    public string? TargetProcessorName { get; init; }

    /// <summary>
    /// The steps the caller may submit next after this step; set when <see cref="ProcessState"/> is
    /// <see cref="ProcessExecutionState.AwaitingStepSelection"/>, otherwise empty.
    /// </summary>
    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    /// <summary>The correlation request id stamped on the step's state by this execution: the id of the request that ran it.</summary>
    public string? StepLatestRequestId { get; init; }

    /// <summary>When this execution was recorded on the step's state (UTC).</summary>
    public DateTimeOffset? StepLastExecuted { get; init; }
}

/// <summary>
/// A request's execution finished: the process state after the request and how many submitted
/// steps it ran.
/// </summary>
/// <remarks>
/// Written once per request after its steps ran (or none did), on its own rather than with a state
/// save. Not written when the request fails with an exception, such as an unknown process id or a
/// concurrent change of the process.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ExecutionCompleted : ProcessEventBase
{
    /// <summary>The process state after the request.</summary>
    public required ProcessExecutionState State { get; init; }

    /// <summary>The step the process requires next, if any.</summary>
    public string? RequiredStep { get; init; }

    /// <summary>The information request presented for <see cref="RequiredStep"/>, if any.</summary>
    public InformationRequest? RequiredInformationRequest { get; init; }

    /// <summary>The processor the process was handed off to, if any.</summary>
    public string? TargetProcessorName { get; init; }

    /// <summary>
    /// The steps the caller may submit next, as stored in the process state; set when
    /// <see cref="State"/> is <see cref="ProcessExecutionState.AwaitingStepSelection"/>, otherwise empty.
    /// </summary>
    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    /// <summary>
    /// The number of submitted steps the request ran, including a step its handler rejected and a
    /// step that failed or was cancelled; 0 when no step ran. Always set by the runtime.
    /// </summary>
    public int? ExecutedStepCount { get; init; }
}

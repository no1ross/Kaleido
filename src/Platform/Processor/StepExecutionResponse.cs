using Kaleido.Processor.Execution;

namespace Kaleido.Platform.Processor;

/// <summary>
/// The response of a per-step execute endpoint (<see cref="ExecuteStepRequest{TProcessStep}"/>) for
/// a step without a typed result; the base of <see cref="StepExecutionResponse{TResponse}"/>.
/// </summary>
/// <remarks>
/// An HTTP 200 does not mean the step completed: inspect <see cref="Status"/>.
/// </remarks>
public record StepExecutionResponse : IHandOffContinuationSource
{
    /// <summary>
    /// The id of the process that was created or continued; also returned in the
    /// <c>X-Kaleido-Process-Id</c> response header.
    /// </summary>
    public required Guid ProcessId
    {
        get;
        init;
    }

    /// <summary>The executed step's registered name (its type name).</summary>
    public required string StepName
    {
        get;
        init;
    }

    /// <summary>
    /// The step the process requires next, including its pending information request when the
    /// process is <see cref="ProcessExecutionState.AwaitingInformation"/>. When this request handed the
    /// process off (<see cref="TargetProcessorName"/> is set), the target's required step as the target
    /// publishes it - its execute URL, its questions and their exchange id (#198); null when the target
    /// requires none.
    /// </summary>
    public ProcessRequiredStep? RequiredStep
    {
        get;
        init;
    }

    /// <summary>
    /// When set, this request handed the process off to this processor: <see cref="RequiredStep"/>,
    /// <see cref="AvailableSteps"/> and <see cref="Notice"/> are its answer, with its execute URLs, and
    /// the step's business messages are its messages (#198). The process continues there; its state is
    /// read with GET /{TargetProcessorName}/{version}/processes/{ProcessId}, preserving the target's published service version.
    /// </summary>
    public string? TargetProcessorName
    {
        get;
        init;
    }

    /// <summary>
    /// What happened to the executed step: <see cref="StepExecutionStatus.Completed"/>,
    /// <see cref="StepExecutionStatus.Rejected"/> or <see cref="StepExecutionStatus.ValidationFailed"/>
    /// (correct the input), <see cref="StepExecutionStatus.Failed"/> (a business failure),
    /// <see cref="StepExecutionStatus.Exception"/> (a defect; retry later),
    /// <see cref="StepExecutionStatus.Canceled"/>, or <see cref="StepExecutionStatus.Pending"/> when it
    /// did not run (the framework messages say why). The step's status, not the HTTP status, says
    /// whether it completed.
    /// </summary>
    public StepExecutionStatus Status
    {
        get;
        init;
    }

    /// <summary>
    /// The steps the caller may run next, each with its execute URL: this processor's, or after a hand-off
    /// the target's (<see cref="TargetProcessorName"/>, #198). Empty when no step is available. Only the
    /// steps this caller may run are listed (#524).
    /// </summary>
    public IReadOnlyCollection<ProcessStepSummary> AvailableSteps
    {
        get;
        init;
    }
        = [];

    /// <summary>
    /// Set, whatever the transport's message options, when the process continues with steps this
    /// caller may not run: its required step (then <c>RequiredStep</c> is null), or every available
    /// step (then <c>AvailableSteps</c> is empty). Its code is <c>pro_not_permitted</c>; the steps are
    /// not named. After a hand-off it is the target's notice, unless the check for this caller sets one
    /// (#198). Null otherwise (#524).
    /// </summary>
    public KaleidoNotice? Notice
    {
        get;
        init;
    }

    /// <summary>Handler-authored messages for consumers, independent of framework diagnostics.</summary>
    public IReadOnlyCollection<ProcessMessage> BusinessMessages
    {
        get;
        init;
    }
        = [];

    /// <summary>Framework diagnostics, empty unless the transport explicitly enables them.</summary>
    public IReadOnlyCollection<ProcessMessage> FrameworkMessages
    {
        get;
        init;
    }
        = [];

    // The target's answer when a handler hands off with this response (#198): what comes next there, by
    // name, its messages and notice. Only a completed step means the target took the process over.
    HandOffContinuation IHandOffContinuationSource.ToHandOffContinuation(string targetProcessorName) =>
        new()
        {
            TargetProcessorName = targetProcessorName,
            Succeeded = Status == StepExecutionStatus.Completed,
            Rejected = Status == StepExecutionStatus.Rejected,
            BusinessMessages = BusinessMessages,
            FrameworkMessages = FrameworkMessages,
            RequiredStep = RequiredStep?.Name,
            InformationRequest = RequiredStep?.InformationRequest,
            AvailableSteps = [.. AvailableSteps.Select(x => x.Name)],
            Notice = Notice,
        };
}

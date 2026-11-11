namespace Kaleido.Process;

/// <summary>
/// The result of <see cref="IProcessorRuntime.ExecuteAsync"/>: the process state after the request
/// and the result of every submitted step.
/// </summary>
/// <remarks>
/// <see cref="State"/>, <see cref="RequiredStep"/>, <see cref="RequiredInformationRequest"/>,
/// <see cref="TargetProcessorName"/> and <see cref="AvailableSteps"/> are taken from the process state
/// after the last step this request ran; when no step changed it, they are the state the request
/// started from.
/// </remarks>
[ExcludeFromCodeCoverage]
internal sealed record ProcessResult
{
    /// <summary>The process instance the request ran against; newly generated when the request created the process without an id.</summary>
    public required Guid ProcessId { get; init; }

    /// <summary>The process state after this request.</summary>
    public required ProcessExecutionState State
    {
        get;
        init;
    }

    /// <summary>
    /// The step the process requires next on this processor, or <see langword="null"/> when it does
    /// not wait for a specific step.
    /// </summary>
    public string? RequiredStep
    {
        get;
        init;
    }

    /// <summary>The pending information request of <see cref="RequiredStep"/>, if any.</summary>
    public InformationRequest? RequiredInformationRequest
    {
        get;
        init;
    }

    /// <summary>
    /// The processor the process was handed off to, or <see langword="null"/> when it stays in this
    /// processor. Set when <see cref="State"/> is <see cref="ProcessExecutionState.HandOff"/>. On the
    /// request that hands off, <see cref="RequiredStep"/>, <see cref="RequiredInformationRequest"/>,
    /// <see cref="AvailableSteps"/> and <see cref="Notice"/> are that processor's answer, by name (#198);
    /// on a later request they are empty.
    /// </summary>
    public string? TargetProcessorName
    {
        get;
        init;
    }

    /// <summary>
    /// The handed-off-to processor's notice on the request that hands off (for example
    /// <c>pro_not_permitted</c>), unchanged; <see langword="null"/> otherwise.
    /// </summary>
    public KaleidoNotice? Notice
    {
        get;
        init;
    }

    /// <summary>
    /// The steps the caller may submit next, as stored in the process state. Set when
    /// <see cref="State"/> is <see cref="ProcessExecutionState.AwaitingStepSelection"/>; empty
    /// otherwise, including for a new process on which no step ran (its initial steps are not stored).
    /// </summary>
    public IReadOnlyCollection<string> AvailableSteps
    {
        get;
        init;
    }
        = [];

    /// <summary>
    /// One result per submitted step, in planning order: the steps that passed validation first, in
    /// dependency order, then the rest.
    /// </summary>
    public IReadOnlyCollection<ProcessStepResult> Steps
    {
        get;
        init;
    }
        = [];
}

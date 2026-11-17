namespace Kaleido.Process;

/// <summary>
/// The durable process state after a local <see cref="IProcessorRuntime.ExecuteAsync"/> call and
/// the outcome of each submitted step. This is a transport-neutral result, not a wire response.
/// </summary>
/// <remarks>
/// <see cref="State"/>, <see cref="RequiredStep"/>, <see cref="RequiredInformationRequest"/>,
/// <see cref="TargetProcessorName"/>, <see cref="TargetProcessorVersion"/> and <see cref="AvailableSteps"/>
/// are taken from the process state after the last step this request ran; when no step changed it,
/// they are the state the request started from. <see cref="ServiceVersion"/> identifies this service,
/// while <see cref="TargetProcessorVersion"/> pins a handed-off process to the target's version.
/// Inspect <see cref="Steps"/> for submitted steps that were invalid, already satisfied,
/// rejected or canceled: an overall process state alone does not say that every submitted step ran.
/// Use <see cref="ProcessId"/> when submitting a later request to the same process.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ProcessResult
{
    /// <summary>The process instance the request ran against; newly generated when the request created the process without an id.</summary>
    public required Guid ProcessId { get; init; }

    /// <summary>The service version that executed this request; use it to pin a later call to this process.</summary>
    public string? ServiceVersion { get; init; }

    /// <summary>The process's stored execution state after this request; inspect <see cref="Steps"/>
    /// for what happened to each submitted step.</summary>
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

    /// <summary>The target's service version when this request handed the process to another service.</summary>
    public string? TargetProcessorVersion { get; init; }

    /// <summary>
    /// A notice from the target processor on the request that hands off (for example, that some
    /// next steps are not permitted), unchanged; <see langword="null"/> otherwise. It does not
    /// grant the caller access to that target's steps.
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
    /// One <see cref="ProcessStepResult"/> per submitted step in planning order: valid steps first
    /// in dependency order, then those the planner could not admit. An included step may still be
    /// pending if an earlier step stopped execution.
    /// </summary>
    public IReadOnlyCollection<ProcessStepResult> Steps
    {
        get;
        init;
    }
        = [];
}

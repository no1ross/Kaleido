namespace Kaleido.Processor.Execution;

[ExcludeFromCodeCoverage]
internal sealed record ProcessExecutionResult
{
    public required Guid ProcessId { get; init; }

    // The version of the process state the request ended with: the last save's (#552).
    public long Version { get; init; }

    public required ProcessExecutionState State
    {
        get;
        init;
    }

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

    public string? TargetProcessorName
    {
        get;
        init;
    }

    public IReadOnlyCollection<string> AvailableSteps
    {
        get;
        init;
    }
        = [];

    // The target's answer when a step of this request handed the process off (#198); the other
    // properties stay the saved state, which the execution-completed event describes.
    public HandOffContinuation? HandOff
    {
        get;
        init;
    }

    public IReadOnlyList<ProcessExecutionOutcome> Outcomes
    {
        get;
        init;
    }
        = [];
}

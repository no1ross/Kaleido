namespace Kaleido.Processor.Execution;

[ExcludeFromCodeCoverage]
public sealed record ProcessExecutionResult
{
    public required Guid ProcessId { get; init; }

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

    public IReadOnlyList<ProcessExecutionOutcome> Outcomes
    {
        get;
        init;
    }
        = [];
}

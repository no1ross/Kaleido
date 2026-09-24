namespace Kaleido.Http.Process.Contracts;

[ExcludeFromCodeCoverage]
public sealed record ProcessStateResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    public ProcessExecutionState State
    {
        get;
        init;
    }

    /// <summary>
    /// The next required step on the local processor.
    /// Null when <see cref="TargetProcessorName"/> is set — call the target processor's
    /// state endpoint instead to get the authoritative required step.
    /// </summary>
    public string? RequiredStep
    {
        get;
        init;
    }

    /// <summary>
    /// When set, the process has been handed off to this processor.
    /// The consumer must call GET /{TargetProcessorName}/processes/{ProcessId} to continue.
    /// <see cref="RequiredStep"/> will be null in this case.
    /// </summary>
    public string? TargetProcessorName
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepSummary> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessStepHistory> Steps
    {
        get;
        init;
    }
        = [];

    public DateTimeOffset CreatedUtc
    {
        get;
        init;
    }

    public DateTimeOffset UpdatedUtc
    {
        get;
        init;
    }
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepHistory
{
    public required string StepName
    {
        get;
        init;
    }

    public string Version
    {
        get;
        init;
    }
        = string.Empty;

    public required StepExecutionStatus Status
    {
        get;
        init;
    }

    public DateTimeOffset? LastExecuted
    {
        get;
        init;
    }
}

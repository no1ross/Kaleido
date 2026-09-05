namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessExecutionResponse
{
    public required Guid ProcessId
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

    public IReadOnlyCollection<ProcessExecutionStepResponse> Results
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessExecutionStepResponse
{
    public required string StepName
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessMessage> Messages
    {
        get;
        init;
    }
        = [];

    public required object Response
    {
        get;
        init;
    }
}

public record StepExecutionResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    public required string StepName
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

    public StepExecutionOutcome? Outcome
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

    public IReadOnlyCollection<ProcessMessage> Messages
    {
        get;
        init;
    }
        = [];
}

public sealed record StepExecutionResponse<TResponse> : StepExecutionResponse
{
    public TResponse? Result
    {
        get;
        init;
    }
}

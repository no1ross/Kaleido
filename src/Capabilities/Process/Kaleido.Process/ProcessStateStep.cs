namespace Kaleido.Process;

/// <summary>The current state and execution evidence of one step in a process snapshot.</summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStateStep
{
    /// <summary>The step's registered name.</summary>
    public required string StepName { get; init; }

    /// <summary>The step's most recently recorded execution status.</summary>
    public required StepExecutionStatus Status { get; init; }

    /// <summary>When this step was first executed, if ever.</summary>
    public DateTimeOffset? FirstExecuted { get; init; }

    /// <summary>The caller of the first execution attempt, if known.</summary>
    public KaleidoCaller? StartedBy { get; init; }

    /// <summary>When this step was most recently executed, if ever.</summary>
    public DateTimeOffset? LastExecuted { get; init; }

    /// <summary>The caller of the most recent execution attempt, if known.</summary>
    public KaleidoCaller? LastUpdatedBy { get; init; }
}

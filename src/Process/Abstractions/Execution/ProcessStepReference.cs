namespace Kaleido.Process.Execution;

/// <summary>
/// Identifies a specific process step within a named processor.
/// Carries only identity — no transport concerns such as URLs.
/// </summary>
public sealed record ProcessStepReference
{
    public required string ProcessorName { get; init; }

    public required string StepName { get; init; }
}

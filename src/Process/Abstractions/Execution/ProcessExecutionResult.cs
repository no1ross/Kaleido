namespace Kaleido.Process.Execution;

public sealed record ProcessExecutionResult
{
    public required Guid ProcessId { get; init; }

    public required ProcessExecutionState State
    {
        get;
        init;
    }

    public ProcessStepReference? RequiredStep
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepReference> AvailableSteps
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

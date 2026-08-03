namespace Kaleido.Process.Participant.Execution;

public sealed record ProcessExecutionResult
{
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

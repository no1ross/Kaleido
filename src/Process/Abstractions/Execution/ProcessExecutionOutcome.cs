namespace Kaleido.Process.Execution;

public sealed record ProcessExecutionOutcome
{
    public required string StepName
    {
        get;
        init;
    }

    public required StepExecutionStatus Status
    {
        get;
        init;
    }

    public required ExecutionDecisionType Decision
    {
        get;
        init;
    }

    public IReadOnlyCollection<StepProcessingMessage> RuntimeMessages
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessMessage> BusinessMessages
    {
        get;
        init;
    }
    = [];

    public object? Response
    {
        get;
        init;
    }
}

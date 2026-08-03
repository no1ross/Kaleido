namespace Kaleido.Process.Participant.Execution;

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

    public IReadOnlyCollection<StepProcessingMessage> Messages
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

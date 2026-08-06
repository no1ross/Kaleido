using Kaleido.Process.Participant;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessExecutionContract
{
    public required string ParticipantProcessId
    {
        get;
        init;
    }

    public string? RequiredStep
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepSummaryContract> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessStepResultContract> Results
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessStepResultContract
{
    public required string StepName
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessMessageContract> Messages
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


public sealed record ProcessExecutionContract<TResponse>
{
    public required string ParticipantProcessId
    {
        get;
        init;
    }

    public required string StepName
    {
        get;
        init;
    }

    public required TResponse Result
    {
        get;
        init;
    }

    public string? RequiredStep
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepSummaryContract> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessMessageContract> Messages
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessMessageContract
{
    public required MessageType Severity
    {
        get;
        init;
    }

    public required string Message
    {
        get;
        init;
    }

    public required string Code
    {
        get;
        init;
    }
}

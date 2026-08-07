using Kaleido.Process.Participant;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessExecutionResponse
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


public sealed record ProcessExecutionResponse<TResponse>
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

public sealed record ProcessMessage
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

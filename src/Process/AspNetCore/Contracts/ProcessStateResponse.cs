using Kaleido.Process.Participant;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessStateResponse
{
    public required Guid ParticipantProcessId
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

    public IReadOnlyCollection<ProcessExecutionHistory> ExecutedSteps
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessExecutionHistory
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

    public DateTimeOffset ExecutedAt
    {
        get;
        init;
    }
}

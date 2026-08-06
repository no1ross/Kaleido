using Kaleido.Process.Participant;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessStateContract
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

    public IReadOnlyCollection<ProcessExecutionHistoryContract> ExecutedSteps
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessExecutionHistoryContract
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

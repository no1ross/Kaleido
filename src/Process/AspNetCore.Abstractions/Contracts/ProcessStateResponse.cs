using Kaleido.Process.Execution;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessStateResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    public ProcessExecutionState State
    {
        get;
        init;
    }

    public ProcessStepInfo? RequiredStep
    {
        get;
        init;
    }

    public IReadOnlyCollection<ProcessStepInfo> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessStepHistory> Steps
    {
        get;
        init;
    }
        = [];

    public DateTimeOffset CreatedUtc
    {
        get;
        init;
    }

    public DateTimeOffset UpdatedUtc
    {
        get;
        init;
    }
}

public sealed record ProcessStepHistory
{
    public required string StepName
    {
        get;
        init;
    }

    public string Version
    {
        get;
        init;
    }
        = string.Empty;

    public required StepExecutionStatus Status
    {
        get;
        init;
    }

    public DateTimeOffset? LastExecuted
    {
        get;
        init;
    }
}

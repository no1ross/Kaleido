using Kaleido.Process.Execution;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessExecutionResponse
{
    public required Guid ProcessId
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

public record StepExecutionResponse
{
    public required Guid ProcessId
    {
        get;
        init;
    }

    public required string StepName
    {
        get;
        init;
    }

    public ProcessStepInfo? RequiredStep
    {
        get;
        init;
    }

    public StepExecutionOutcome? Outcome
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

    public IReadOnlyCollection<ProcessMessage> Messages
    {
        get;
        init;
    }
        = [];
}

public sealed record StepExecutionResponse<TResponse> : StepExecutionResponse
{
    public TResponse? Result
    {
        get;
        init;
    }
}

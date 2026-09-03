using Kaleido.Process.Execution;
using Kaleido.Process.Planning;

namespace Kaleido.Process.Context;

public sealed record StepProcessingRecord
{
    public required string StepName { get; init; }

    public bool Candidate { get; init; }

    public bool Executed { get; init; }

    public StepExecutionStatus Status { get; init; }

    public ProcessExecutionState ProcessState { get; init; }

    public string? RequiredStep { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; }
        = [];

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];

    public DateTimeOffset ProcessedOn { get; init; }
}

public sealed record RequestRecord
{
    public required string RequestId
    {
        get;
        init;
    }

    public DateTimeOffset ProcessedOn
    {
        get;
        init;
    }

    public IReadOnlyCollection<StepProcessingRecord> Steps
    {
        get;
        init;
    }
        = [];
}
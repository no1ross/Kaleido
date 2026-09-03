using Kaleido.Eventing;
using Kaleido.Process.Execution;

namespace Kaleido.Process.Eventing;

[KaleidoEvent(Type = "process.step-completed.v1")]
public sealed record StepCompleted : ProcessEventBase
{
    public required string StepName { get; init; }

    public required string StepVersion { get; init; }

    public object? Request { get; init; }

    public object? Response { get; init; }

    public required ExecutionDecisionType DecisionType { get; init; }

    public required StepExecutionStatus ExecutionStatus { get; init; }

    public required StepExecutionOutcome Outcome { get; init; }

    public IReadOnlyCollection<ProcessMessage> BusinessMessages { get; init; } = [];

    public IReadOnlyCollection<StepProcessingMessage> RuntimeMessages { get; init; } = [];

    public required ProcessExecutionState ProcessState { get; init; }

    public string? RequiredStep { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    public string? StepLatestRequestId { get; init; }

    public DateTimeOffset? StepLastExecuted { get; init; }
}

using Kaleido.Eventing;
using Kaleido.Process.Execution;

namespace Kaleido.Process.Eventing;

[KaleidoEvent(Type = "process.plan-built.v1")]
public sealed record PlanBuilt : ProcessEventBase
{
    public required ProcessExecutionState State { get; init; }

    public string? RequiredStep { get; init; }

    public IReadOnlyCollection<string> AvailableSteps { get; init; } = [];

    public IReadOnlyCollection<string> SubmittedStepNames { get; init; } = [];

    public required int SubmittedStepCount { get; init; }

    public required int CandidateCount { get; init; }

    public required int ExecutableCount { get; init; }

    public IReadOnlyCollection<PlanBuiltCandidate> Candidates { get; init; } = [];
}

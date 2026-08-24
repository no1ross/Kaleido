using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Planning;

namespace Kaleido.Process.Eventing;

public sealed record PlanBuiltCandidate
{
    public required string StepName { get; init; }

    public required string StepVersion { get; init; }

    public required StepCandidateStatus CandidateStatus { get; init; }

    public required bool IncludedInExecutionPlan { get; init; }

    public IReadOnlyCollection<PlanBuiltCandidateMessage> Messages { get; init; } = [];
}

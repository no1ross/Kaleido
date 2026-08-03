using Kaleido.Process.Participant.Context;

namespace Kaleido.Process.Participant.Planning;

internal interface IStepCandidateConsistencyChecker
{
    void Validate(IReadOnlyCollection<StepCandidate> candidates, ParticipantContext context);
}

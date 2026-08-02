namespace Kaleido.Process.Participant.Planning;

internal interface IStepCandidateBuilder
{
    IReadOnlyCollection<StepCandidate> Build(ParticipantRequest request);
}



namespace Kaleido.Process.Participant.Planning;

internal interface IStepCandidatePlanner
{
    IReadOnlyCollection<StepCandidate> Build(IReadOnlyCollection<StepCandidate> candidates);
}

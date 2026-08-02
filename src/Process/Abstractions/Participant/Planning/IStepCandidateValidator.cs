namespace Kaleido.Process.Participant.Planning;

internal interface IStepCandidateValidator
{
    void Validate(IReadOnlyCollection<StepCandidate> candidates);
}

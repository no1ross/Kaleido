namespace Kaleido.Process.Planning;

internal interface IStepCandidateValidator
{
    void Validate(IReadOnlyCollection<StepCandidate> candidates);
}

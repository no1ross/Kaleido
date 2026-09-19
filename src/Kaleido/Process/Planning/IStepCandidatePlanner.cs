namespace Kaleido.Process.Planning;

internal interface IStepCandidatePlanner
{
    IReadOnlyCollection<StepCandidate> Build(IReadOnlyCollection<StepCandidate> candidates);
}

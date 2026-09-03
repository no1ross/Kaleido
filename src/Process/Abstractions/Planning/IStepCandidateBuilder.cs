namespace Kaleido.Process.Planning;

internal interface IStepCandidateBuilder
{
    IReadOnlyCollection<StepCandidate> Build(ProcessorRequest request);
}



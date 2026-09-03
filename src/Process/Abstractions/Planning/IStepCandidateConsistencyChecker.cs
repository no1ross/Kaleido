using Kaleido.Process.Context;

namespace Kaleido.Process.Planning;

internal interface IStepCandidateConsistencyChecker
{
    void Validate(IReadOnlyCollection<StepCandidate> candidates, ProcessorContext context);
}

using Kaleido.Processor.Registry;

namespace Kaleido.Processor.Planning;

internal interface IStepCandidateBuilder
{
    IReadOnlyCollection<StepCandidate> Build(ProcessorRequest request);
}

internal sealed class StepCandidateBuilder(
    IProcessorStepRegistry registry)
    : IStepCandidateBuilder
{

    public IReadOnlyCollection<StepCandidate> Build(ProcessorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidates = new List<StepCandidate>();

        foreach (var step in request.Steps)
        {
            var registration = registry.Find(step.Key);

            if (registration is null)
            {
                candidates.Add(
                    StepCandidate.Invalid(
                        step.Key,
                        ProcessorErrorCodes.UnknownStep,
                        $"Process step '{step.Key}' is not registered."));

                continue;
            }

            var candidate =
                new StepCandidate
                {
                    StepName = step.Key,
                    Registration = registration,
                };

            // Steps arrive typed: a transport reads its wire format into the step type before core
            // sees it, so core never parses input (#344).
            if (registration.StepType.IsInstanceOfType(step.Value))
            {
                candidate.Step = step.Value;
                candidate.Status = StepCandidateStatus.Built;
            }
            else
            {
                candidate.MarkInvalid(
                    ProcessorErrorCodes.InvalidRequest,
                    $"Process step '{step.Key}' must be submitted as a '{registration.StepType.Name}'.");
            }

            candidates.Add(candidate);
        }

        return candidates;
    }
}

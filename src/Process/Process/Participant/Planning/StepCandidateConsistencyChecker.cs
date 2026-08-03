using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Registry;
using Kaleido.Json;

namespace Kaleido.Process.Participant.Planning;

internal interface IStepCandidateConsistencyChecker
{
    void Validate(IReadOnlyCollection<StepCandidate> candidates, ParticipantContext context);
}

internal class StepCandidateConsistencyChecker : IStepCandidateConsistencyChecker
{
    private readonly IProcessStepRegistry _registry;

    public StepCandidateConsistencyChecker(IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public void Validate(
        IReadOnlyCollection<StepCandidate> candidates, ParticipantContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var candidate in candidates)
        {
            if (candidate.Status ==
                StepCandidateStatus.Invalid)
            {
                continue;
            }

            ValidateHistoricalConsistency(
                candidate,
                context);

            if (candidate.Status ==
                StepCandidateStatus.Invalid)
            {
                continue;
            }

            ValidateDependencyConsistency(
                candidate,
                candidates,
                context);
        }
    }

    private static void ValidateHistoricalConsistency(
        StepCandidate candidate,
        ParticipantContext context)
    {
        var historicalStep =
            context.Steps.FirstOrDefault(
                x => string.Equals(
                    x.StepName,
                    candidate.Registration!.Metadata.Name,
                    StringComparison.OrdinalIgnoreCase));

        if (historicalStep is null)
        {
            return;
        }

        if (historicalStep.Status ==
            StepExecutionStatus.Completed)
        {
            candidate.Status =
                StepCandidateStatus.Satisfied;
        }
    }

    private void ValidateDependencyConsistency(
        StepCandidate candidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context)
    {
        var dependencies =
            _registry.GetDependencies(
                candidate.Registration!.StepType);

        foreach (var dependency in dependencies)
        {
            if (DependencySatisfiedByHistory(
                    dependency,
                    context))
            {
                continue;
            }

            if (DependencySatisfiedByCandidate(
                    dependency,
                    candidates))
            {
                continue;
            }

            candidate.MarkInvalid(
                StepProcessingMessageCode.DependencyNotSatisfied,
                $"Process step '{candidate.Registration.Metadata.Name}' " +
                $"requires process step '{dependency.Metadata.Name}' " +
                $"to be completed before it can execute.");
        }
    }

    private static bool DependencySatisfiedByHistory(
        ProcessStepRegistration dependency,
        ParticipantContext context)
    {
        var historicalStep =
            context.Steps.FirstOrDefault(
                x => string.Equals(
                    x.StepName,
                    dependency.Metadata.Name,
                    StringComparison.OrdinalIgnoreCase));

        if (historicalStep is null)
        {
            return false;
        }

        return historicalStep.Status ==
               StepExecutionStatus.Completed;
    }

    private static bool DependencySatisfiedByCandidate(
        ProcessStepRegistration dependency,
        IReadOnlyCollection<StepCandidate> candidates)
    {
        var dependencyCandidate =
            candidates.FirstOrDefault(
                x => x.Registration!.StepType ==
                     dependency.StepType);

        if (dependencyCandidate is null)
        {
            return false;
        }

        // Any candidate that successfully participates in planning
        // is considered capable of satisfying dependencies.
        // Only invalid candidates are excluded.
        return dependencyCandidate.Status !=
               StepCandidateStatus.Invalid;
    }
}

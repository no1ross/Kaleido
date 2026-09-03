using Kaleido.Json;
using Kaleido.Process.Context;
using Kaleido.Process.Registry;

namespace Kaleido.Process.Planning;

internal class StepCandidateConsistencyChecker : IStepCandidateConsistencyChecker
{
    public StepCandidateConsistencyChecker()
    {
    }

    public void Validate(
        IReadOnlyCollection<StepCandidate> candidates, ProcessorContext context)
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
        ProcessorContext context)
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

        if (candidate.Registration!.Repeatable.Enabled)
        {
            candidate.AddInformation(
                StepProcessingMessageCode.RepeatableStep,
                $"Step '{candidate.StepName}' is repeatable and remains eligible for execution despite prior execution history.");

            return;
        }

        if (historicalStep.Status ==
            StepExecutionStatus.Completed)
        {
            candidate.Status =
                StepCandidateStatus.Satisfied;

            candidate.AddInformation(
                StepProcessingMessageCode.AlreadyProcessed,
                $"Step '{candidate.StepName}' was previously completed and did not require execution.");
        }
    }

    private void ValidateDependencyConsistency(
        StepCandidate candidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {
        var dependencies =
            candidate.Registration!.Dependencies;

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
        ProcessorContext context)
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

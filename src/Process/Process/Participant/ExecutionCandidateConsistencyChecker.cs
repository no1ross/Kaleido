using System.Text.Json;

namespace Kaleido.Process.Participant.Execution;

internal static class ExecutionCandidateConsistencyChecker
{
    public static void Validate(
        IReadOnlyCollection<ExecutionCandidate> candidates,
        ParticipantContext context,
        IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);

        foreach (var candidate in candidates)
        {
            if (candidate.Status ==
                ExecutionCandidateStatus.Invalid)
            {
                continue;
            }

            ValidateHistoricalConsistency(
                candidate,
                context);

            if (candidate.Status ==
                ExecutionCandidateStatus.Invalid)
            {
                continue;
            }

            ValidateDependencyConsistency(
                candidate,
                candidates,
                context,
                registry);
        }
    }

    private static void ValidateHistoricalConsistency(
        ExecutionCandidate candidate,
        ParticipantContext context)
    {
        var historicalStep =
            context.ProcessSteps.FirstOrDefault(
                x => string.Equals(
                    x.StepName,
                    candidate.Registration!.Metadata.Name,
                    StringComparison.OrdinalIgnoreCase));

        if (historicalStep is null)
        {
            return;
        }

        if (historicalStep.Outcome !=
            ProcessStepOutcome.Completed)
        {
            return;
        }

        var latestRequest =
            historicalStep.Requests
                .OrderByDescending(x => x.ProcessedOn)
                .FirstOrDefault();

        if (latestRequest?.Step is null)
        {
            return;
        }

        if (ProcessStepRequestComparer.AreEqual(
                latestRequest.Step,
                candidate.Step))
        {
            candidate.Status =
                ExecutionCandidateStatus.Satisfied;

            return;
        }

        candidate.Status =
            ExecutionCandidateStatus.Invalid;

        candidate.AddMessage(
            ProcessStepMessage.Error(
                ProcessStepMessageCode.ConsistencyViolation,
                $"Process step '{candidate.Registration!.Metadata.Name}' " +
                $"was previously completed with different values."));
    }

    private static void ValidateDependencyConsistency(
        ExecutionCandidate candidate,
        IReadOnlyCollection<ExecutionCandidate> candidates,
        ParticipantContext context,
        IProcessStepRegistry registry)
    {
        var dependencies =
            registry.GetDependencies(
                candidate.Registration!.StepType);

        foreach (var dependency in dependencies)
        {
            if (DependencySatisfiedByHistory(
                    dependency,
                    context))
            {
                continue;
            }

            if (DependencySatisfiedByRequest(
                    dependency,
                    candidates))
            {
                continue;
            }

            candidate.Status =
                ExecutionCandidateStatus.Invalid;

            candidate.AddMessage(
                ProcessStepMessage.Error(
                    ProcessStepMessageCode.DependencyNotSatisfied,
                    $"Process step '{candidate.Registration.Metadata.Name}' " +
                    $"requires process step '{dependency.Metadata.Name}' " +
                    $"to be completed before it can execute."));
        }
    }

    private static bool DependencySatisfiedByHistory(
        ProcessStepRegistration dependency,
        ParticipantContext context)
    {
        var historicalStep =
            context.ProcessSteps.FirstOrDefault(
                x => string.Equals(
                    x.StepName,
                    dependency.Metadata.Name,
                    StringComparison.OrdinalIgnoreCase));

        if (historicalStep is null)
        {
            return false;
        }

        return historicalStep.Outcome ==
               ProcessStepOutcome.Completed;
    }

    private static bool DependencySatisfiedByRequest(
        ProcessStepRegistration dependency,
        IReadOnlyCollection<ExecutionCandidate> candidates)
    {
        var dependencyCandidate =
            candidates.FirstOrDefault(
                x => x.Registration!.StepType ==
                     dependency.StepType);

        if (dependencyCandidate is null)
        {
            return false;
        }

        return dependencyCandidate.Status !=
               ExecutionCandidateStatus.Invalid;
    }
}


internal static class ProcessStepRequestComparer
{
    private static readonly JsonSerializerOptions
        SerializerOptions =
            new()
            {
                WriteIndented = false
            };

    public static bool AreEqual(
        object? previousStep,
        object? currentStep)
    {
        if (ReferenceEquals(
                previousStep,
                currentStep))
        {
            return true;
        }

        if (previousStep is null ||
            currentStep is null)
        {
            return false;
        }

        if (previousStep.GetType() !=
            currentStep.GetType())
        {
            return false;
        }

        var previousJson =
            JsonSerializer.Serialize(
                previousStep,
                SerializerOptions);

        var currentJson =
            JsonSerializer.Serialize(
                currentStep,
                SerializerOptions);

        return string.Equals(
            previousJson,
            currentJson,
            StringComparison.Ordinal);
    }
}
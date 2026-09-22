namespace Kaleido.Process.Planning;

internal interface IStepCandidatePlanner
{
    IReadOnlyCollection<StepCandidate> Build(IReadOnlyCollection<StepCandidate> candidates);
}

internal sealed class StepCandidatePlanner : IStepCandidatePlanner
{
    public IReadOnlyCollection<StepCandidate> Build(
        IReadOnlyCollection<StepCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var executableCandidates =
            candidates
                .Where(IsExecutable)
                .ToArray();

        var ordered =
            OrderByDependencies(
                executableCandidates);

        foreach (var candidate in ordered)
        {
            candidate.IncludedInExecutionPlan = true;
        }

        return ordered
            .Concat(candidates.Except(executableCandidates))
            .Distinct()
            .ToArray();
    }

    private static bool IsExecutable(
        StepCandidate candidate)
    {
        return candidate.Status ==
               StepCandidateStatus.Built;
    }

    private static IReadOnlyCollection<StepCandidate> OrderByDependencies(
        IReadOnlyCollection<StepCandidate> candidates)
    {
        var candidatesByType =
            candidates.ToDictionary(
                x => (x.Registration ?? throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.MissingRegistration,
                    $"StepCandidate '{x.StepName}' has no Registration during dependency ordering.")).StepType);

        var ordered =
            new List<StepCandidate>();

        var visited =
            new HashSet<Type>();

        foreach (var candidate in candidates)
        {
            Visit(
                candidate,
                candidatesByType,
                visited,
                ordered);
        }

        return ordered;
    }

    private static void Visit(
        StepCandidate candidate,
        IReadOnlyDictionary<Type, StepCandidate> candidatesByType,
        HashSet<Type> visited,
        List<StepCandidate> ordered)
    {
        var registration =
            candidate.Registration
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"StepCandidate '{candidate.StepName}' has no Registration during dependency ordering.");

        var stepType = registration.StepType;

        if (!visited.Add(stepType))
        {
            return;
        }

        var dependencies = registration.Dependencies;

        foreach (var dependency in dependencies)
        {
            if (!candidatesByType.TryGetValue(
                    dependency.StepType,
                    out var dependencyCandidate))
            {
                continue;
            }

            Visit(
                dependencyCandidate,
                candidatesByType,
                visited,
                ordered);
        }

        ordered.Add(candidate);
    }
}

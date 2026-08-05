namespace Kaleido.Process.Participant.Planning;

internal class StepCandidatePlanner : IStepCandidatePlanner
{
    public StepCandidatePlanner()
    {
    }

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
                x => x.Registration!.StepType);

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
        var stepType =
            candidate.Registration!.StepType;

        if (!visited.Add(stepType))
        {
            return;
        }

        var dependencies =
            candidate.Registration!.Dependencies;

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

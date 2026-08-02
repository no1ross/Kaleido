namespace Kaleido.Process.Participant.Planning;

internal interface IStepCandidatePlanner
{
    IReadOnlyCollection<StepCandidate> Build(IReadOnlyCollection<StepCandidate> candidates);
}

internal class StepCandidatePlanner : IStepCandidatePlanner
{
    private readonly IProcessStepRegistry _registry;

    public StepCandidatePlanner(IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
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
                executableCandidates,
                _registry);

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
        IReadOnlyCollection<StepCandidate> candidates,
        IProcessStepRegistry registry)
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
                registry,
                visited,
                ordered);
        }

        return ordered;
    }

    private static void Visit(
        StepCandidate candidate,
        IReadOnlyDictionary<Type, StepCandidate> candidatesByType,
        IProcessStepRegistry registry,
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
            registry.GetDependencies(
                stepType);

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
                registry,
                visited,
                ordered);
        }

        ordered.Add(candidate);
    }
}

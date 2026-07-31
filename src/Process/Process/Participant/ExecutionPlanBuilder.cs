namespace Kaleido.Process.Participant.Execution;

internal static class ExecutionPlanBuilder
{
    public static ExecutionPlan Build(
        IReadOnlyCollection<ExecutionCandidate> candidates,
        IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(registry);

        var executableCandidates =
            candidates
                .Where(IsExecutable)
                .ToArray();

        if (executableCandidates.Length == 0)
        {
            return new ExecutionPlan();
        }

        var ordered =
            OrderByDependencies(
                executableCandidates,
                registry);

        return new ExecutionPlan
        {
            Steps = ordered.Select(x => 
                new ExecutionPlanItem { 
                    StepName = x.StepName 
                }).ToArray(),
        };
    }

    private static bool IsExecutable(
        ExecutionCandidate candidate)
    {
        return candidate.Status ==
               ExecutionCandidateStatus.Built;
    }

    private static IReadOnlyCollection<ExecutionCandidate> OrderByDependencies(
        IReadOnlyCollection<ExecutionCandidate> candidates,
        IProcessStepRegistry registry)
    {
        var candidatesByType =
            candidates.ToDictionary(
                x => x.Registration!.StepType);

        var ordered =
            new List<ExecutionCandidate>();

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
        ExecutionCandidate candidate,
        IReadOnlyDictionary<Type, ExecutionCandidate> candidatesByType,
        IProcessStepRegistry registry,
        HashSet<Type> visited,
        List<ExecutionCandidate> ordered)
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

internal sealed record ExecutionPlan
{
    public IReadOnlyCollection<ExecutionPlanItem> Steps
    {
        get;
        init;
    }
        = [];
}

internal sealed record ExecutionPlanItem
{
    public required string StepName
    {
        get;
        init;
    }
}
namespace Kaleido.Process.Participant.Execution;

internal sealed class ExecutionPlanner
{
    private readonly IProcessStepRegistry _registry;

    public ExecutionPlanner(
        IProcessStepRegistry registry)
    {
        _registry =
            registry
            ?? throw new ArgumentNullException(
                nameof(registry));
    }

    public ExecutionPlanResult BuildPlan(
        ParticipantRequest request,
        ParticipantContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var candidates =
            ExecutionCandidateBuilder
                .Build(
                    request,
                    _registry)
                .Candidates;

        ExecutionCandidateValidator
            .Validate(candidates);

        ExecutionCandidateConsistencyChecker
            .Validate(
                candidates,
                context,
                _registry);

        var executionPlan =
            ExecutionPlanBuilder
                .Build(
                    candidates,
                    _registry);

        return new ExecutionPlanResult
        {
            Candidates = candidates,
            ExecutionPlan = executionPlan
        };
    }
}

internal sealed record ExecutionPlanResult
{
    public required IReadOnlyCollection<ExecutionCandidate> Candidates
    {
        get;
        init;
    }

    public required ExecutionPlan ExecutionPlan
    {
        get;
        init;
    }
}
using Kaleido.Process.Participant.Context;

namespace Kaleido.Process.Participant.Planning;

internal interface IExecutionPlanner
{
    ExecutionPlanResult BuildPlan(ParticipantRequest request, ParticipantContext context);
}

internal sealed class ExecutionPlanner : IExecutionPlanner
{
    private readonly IStepCandidateBuilder _candidateBuilder;

    private readonly IStepCandidateValidator _candidateValidator;

    private readonly IStepCandidateConsistencyChecker _candidateConsistencyChecker;

    private readonly IStepCandidatePlanner _stepCandidatePlanner;

    public ExecutionPlanner(
        IStepCandidateBuilder candidateBuilder,
        IStepCandidateValidator candidateValidator,
        IStepCandidateConsistencyChecker candidateConsistencyChecker,
        IStepCandidatePlanner stepCandidatePlanner)
    {
        ArgumentNullException.ThrowIfNull(candidateBuilder);
        ArgumentNullException.ThrowIfNull(candidateValidator);
        ArgumentNullException.ThrowIfNull(candidateConsistencyChecker);
        ArgumentNullException.ThrowIfNull(stepCandidatePlanner);

        _candidateBuilder = candidateBuilder;
        _candidateValidator = candidateValidator;
        _candidateConsistencyChecker = candidateConsistencyChecker;
        _stepCandidatePlanner = stepCandidatePlanner;
    }

    public ExecutionPlanResult BuildPlan(
        ParticipantRequest request,
        ParticipantContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var candidates =
            _candidateBuilder.Build(request);

        _candidateValidator.Validate(candidates);

        _candidateConsistencyChecker.Validate(
            candidates,
            context);

        var orderedCandidates =
            _stepCandidatePlanner.Build(candidates);

        return new ExecutionPlanResult
        {
            Candidates = orderedCandidates
        };
    }
}

using Kaleido.Process.Context;

namespace Kaleido.Process.Planning;

internal interface IProcessPlanner
{
    ExecutionPlanResult BuildPlan(ProcessorRequest request, ProcessorContext context);
}

internal sealed class ProcessPlanner(
    IStepCandidateBuilder candidateBuilder,
    IStepCandidateValidator candidateValidator,
    IStepCandidateConsistencyChecker candidateConsistencyChecker,
    IStepCandidatePlanner stepCandidatePlanner)
    : IProcessPlanner
{

    public ExecutionPlanResult BuildPlan(
        ProcessorRequest request,
        ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var candidates =
            candidateBuilder.Build(request);

        candidateValidator.Validate(candidates);

        candidateConsistencyChecker.Validate(
            candidates,
            context);

        var orderedCandidates =
            stepCandidatePlanner.Build(candidates);

        return new ExecutionPlanResult
        {
            Candidates = orderedCandidates
        };
    }
}

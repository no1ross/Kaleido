using Kaleido.Process.Context;

namespace Kaleido.Process.Planning;

internal interface IProcessorPlanner
{
    ExecutionPlanResult BuildPlan(ProcessorRequest request, ProcessContext context);
}

internal sealed class ProcessorPlanner(
    IStepCandidateBuilder candidateBuilder,
    IStepCandidateValidator candidateValidator,
    IStepCandidateConsistencyChecker candidateConsistencyChecker,
    IStepCandidatePlanner stepCandidatePlanner,
    IStepCandidateNextStepChecker nextStepChecker)
    : IProcessorPlanner
{

    public ExecutionPlanResult BuildPlan(
        ProcessorRequest request,
        ProcessContext context)
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

        nextStepChecker.Check(
            orderedCandidates,
            context);

        return new ExecutionPlanResult
        {
            Candidates = orderedCandidates,
        };
    }
}

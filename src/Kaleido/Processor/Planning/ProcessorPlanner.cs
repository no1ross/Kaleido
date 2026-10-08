using Kaleido.Processor.Context;

namespace Kaleido.Processor.Planning;

internal interface IProcessorPlanner
{
    ExecutionPlanResult BuildPlan(ProcessorRequest request, ProcessorContext context);
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

        nextStepChecker.Check(
            orderedCandidates,
            context);

        return new ExecutionPlanResult
        {
            Candidates = orderedCandidates
        };
    }
}

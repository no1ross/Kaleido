using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Planning;

namespace Kaleido.Process.Eventing;

internal interface IProcessEventFactory
{
    ProcessCreated CreateProcessCreated(
        ProcessorContext context,
        ProcessRequest request);

    PlanBuilt CreatePlanBuilt(
        ProcessorContext context,
        ProcessRequest request,
        ExecutionPlanResult plan,
        int executableCount);

    StepCompleted CreateStepCompleted(
        ProcessorContext context,
        StepCandidate candidate,
        ProcessExecutionOutcome outcome,
        ProcessStepInvokerResult result,
        StepExecutionOutcome executionOutcome);

    ExecutionCompleted CreateExecutionCompleted(
        ProcessExecutionResult executionResult);
}

internal sealed class ProcessEventFactory
    : IProcessEventFactory
{
    public ProcessCreated CreateProcessCreated(
        ProcessorContext context,
        ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var submittedStepNames =
            request.Processor.Steps.Keys.ToArray();

        return new ProcessCreated
        {
            ProcessId = context.ProcessId,
            OccurredOn = DateTimeOffset.UtcNow,
            State = context.State,
            CreatedUtc = context.CreatedUtc,
            UpdatedUtc = context.UpdatedUtc,
            SubmittedStepNames = submittedStepNames,
            SubmittedStepCount = submittedStepNames.Length
        };
    }

    public PlanBuilt CreatePlanBuilt(
        ProcessorContext context,
        ProcessRequest request,
        ExecutionPlanResult plan,
        int executableCount)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);

        var submittedStepNames =
            request.Processor.Steps.Keys.ToArray();

        return new PlanBuilt
        {
            ProcessId = context.ProcessId,
            OccurredOn = DateTimeOffset.UtcNow,
            State = context.State,
            RequiredStep = context.RequiredStep,
            AvailableSteps = context.AvailableSteps,
            SubmittedStepNames = submittedStepNames,
            SubmittedStepCount = submittedStepNames.Length,
            CandidateCount = plan.Candidates.Count,
            ExecutableCount = executableCount,
            Candidates =
                plan.Candidates
                    .Select(
                        candidate => new PlanBuiltCandidate
                        {
                            StepName = candidate.StepName,
                            StepVersion = candidate.Registration?.Metadata.Version ?? string.Empty,
                            CandidateStatus = candidate.Status,
                            IncludedInExecutionPlan = candidate.IncludedInExecutionPlan,
                            Messages =
                                candidate.Messages
                                    .Select(
                                        message => new PlanBuiltCandidateMessage
                                        {
                                            Type = message.Type,
                                            Code = message.Code,
                                            Message = message.Message
                                        })
                                    .ToArray()
                        })
                    .ToArray()
        };
    }

    public StepCompleted CreateStepCompleted(
        ProcessorContext context,
        StepCandidate candidate,
        ProcessExecutionOutcome outcome,
        ProcessStepInvokerResult result,
        StepExecutionOutcome executionOutcome)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(result);

        var stepContext =
            context.FindStep(candidate.StepName);

        return new StepCompleted
        {
            ProcessId = context.ProcessId,
            OccurredOn = DateTimeOffset.UtcNow,
            StepName = candidate.StepName,
            StepVersion = stepContext?.Version ?? candidate.Registration?.Metadata.Version ?? string.Empty,
            Request = candidate.Step,
            Response = outcome.Response,
            DecisionType = outcome.Decision,
            ExecutionStatus = outcome.Status,
            Outcome = executionOutcome,
            BusinessMessages = outcome.BusinessMessages,
            RuntimeMessages = outcome.RuntimeMessages,
            ProcessState = context.State,
            RequiredStep = context.RequiredStep,
            AvailableSteps = context.AvailableSteps,
            StepLatestRequestId = stepContext?.LatestRequestId,
            StepLastExecuted = stepContext?.LastExecuted
        };
    }

    public ExecutionCompleted CreateExecutionCompleted(
        ProcessExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        return new ExecutionCompleted
        {
            ProcessId = executionResult.ProcessId,
            OccurredOn = DateTimeOffset.UtcNow,
            State = executionResult.State,
            RequiredStep = executionResult.RequiredStep,
            AvailableSteps = executionResult.AvailableSteps,
            ExecutedStepCount = executionResult.Outcomes.Count
        };
    }
}

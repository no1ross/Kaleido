using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using Kaleido.Process.Planning;

namespace Kaleido.Process.Eventing;

internal interface IProcessEventFactory
{
    KaleidoEventEnvelope<ProcessCreated, ProcessEventContext> CreateProcessCreated(
        KaleidoCorrelationContext correlation,
        ProcessorContext context,
        ProcessRequest request);

    KaleidoEventEnvelope<PlanBuilt, ProcessEventContext> CreatePlanBuilt(
        KaleidoCorrelationContext correlation,
        ProcessorContext context,
        ProcessRequest request,
        ExecutionPlanResult plan,
        int executableCount);

    KaleidoEventEnvelope<StepCompleted, ProcessEventContext> CreateStepCompleted(
        KaleidoCorrelationContext correlation,
        ProcessorContext context,
        StepCandidate candidate,
        ProcessExecutionOutcome outcome,
        ProcessStepInvokerResult result);

    KaleidoEventEnvelope<ExecutionCompleted, ProcessEventContext> CreateExecutionCompleted(
        KaleidoCorrelationContext correlation,
        ProcessorContext context,
        ProcessExecutionResult executionResult);
}

internal sealed class ProcessEventFactory(
    KaleidoServiceOptions serviceOptions)
    : IProcessEventFactory
{
    private ProcessEventContext CreateContext(KaleidoCorrelationContext correlation, string stepName, Guid processId) =>
        new()
        {
            RequestId = correlation.RequestId,
            ServiceName = serviceOptions.ServiceName,
            ProcessId = processId,
            StepName = stepName,
            ProcessorInstanceId = serviceOptions.InstanceId.ToString(),
            SourceProcessorName = correlation.SourceProcessorName ?? serviceOptions.ServiceName
        };

    public KaleidoEventEnvelope<ProcessCreated, ProcessEventContext> CreateProcessCreated(
        KaleidoCorrelationContext correlation,
        ProcessorContext context,
        ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var submittedStepNames =
            request.Processor.Steps.Keys.ToArray();

        var @event = new ProcessCreated
        {
            OccurredOn = DateTimeOffset.UtcNow,
            State = context.State,
            CreatedUtc = context.CreatedUtc,
            UpdatedUtc = context.UpdatedUtc,
            SubmittedStepNames = submittedStepNames,
            SubmittedStepCount = submittedStepNames.Length
        };

        return new KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>
        {
            EventType = KaleidoEventTypes.ProcessCreated,
            Context = CreateContext(correlation, submittedStepNames.FirstOrDefault() ?? string.Empty, context.ProcessId),
            Event = @event
        };
    }

    public KaleidoEventEnvelope<PlanBuilt, ProcessEventContext> CreatePlanBuilt(
        KaleidoCorrelationContext correlation,
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

        var @event = new PlanBuilt
        {
            OccurredOn = DateTimeOffset.UtcNow,
            State = context.State,
            RequiredStep = context.RequiredStep,
            TargetProcessorName = context.TargetProcessorName,
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

        return new KaleidoEventEnvelope<PlanBuilt, ProcessEventContext>
        {
            EventType = KaleidoEventTypes.PlanBuilt,
            Context = CreateContext(correlation, submittedStepNames.FirstOrDefault() ?? string.Empty, context.ProcessId),
            Event = @event
        };
    }

    public KaleidoEventEnvelope<StepCompleted, ProcessEventContext> CreateStepCompleted(
        KaleidoCorrelationContext correlation,
        ProcessorContext context,
        StepCandidate candidate,
        ProcessExecutionOutcome outcome,
        ProcessStepInvokerResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(result);

        var stepContext =
            context.FindStep(candidate.StepName);

        var @event = new StepCompleted
        {
            OccurredOn = DateTimeOffset.UtcNow,
            StepName = candidate.StepName,
            StepVersion = stepContext?.Version ?? candidate.Registration?.Metadata.Version ?? string.Empty,
            Request = candidate.Step,
            Response = outcome.Response,
            DecisionType = outcome.Decision,
            ExecutionStatus = outcome.Status,
            Outcome = outcome.Outcome,
            BusinessMessages = outcome.BusinessMessages,
            RuntimeMessages = outcome.RuntimeMessages,
            ProcessState = context.State,
            RequiredStep = context.RequiredStep,
            TargetProcessorName = context.TargetProcessorName,
            AvailableSteps = context.AvailableSteps,
            StepLatestRequestId = stepContext?.LatestRequestId,
            StepLastExecuted = stepContext?.LastExecuted
        };

        return new KaleidoEventEnvelope<StepCompleted, ProcessEventContext>
        {
            EventType = KaleidoEventTypes.StepCompleted,
            Context = CreateContext(correlation, candidate.StepName, context.ProcessId),
            Event = @event
        };
    }

    public KaleidoEventEnvelope<ExecutionCompleted, ProcessEventContext> CreateExecutionCompleted(
        KaleidoCorrelationContext correlation,
        ProcessorContext context,
        ProcessExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executionResult);

        var @event = new ExecutionCompleted
        {
            OccurredOn = DateTimeOffset.UtcNow,
            State = executionResult.State,
            RequiredStep = executionResult.RequiredStep,
            TargetProcessorName = executionResult.TargetProcessorName,
            AvailableSteps = executionResult.AvailableSteps,
            ExecutedStepCount = executionResult.Outcomes.Count
        };

        return new KaleidoEventEnvelope<ExecutionCompleted, ProcessEventContext>
        {
            EventType = KaleidoEventTypes.ExecutionCompleted,
            Context = CreateContext(correlation, string.Empty, executionResult.ProcessId),
            Event = @event
        };
    }
}

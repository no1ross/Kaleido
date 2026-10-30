using Kaleido.Processor.Registry;

namespace Kaleido.Processor.Context;

internal interface IProcessorStateUpdater
{
    ProcessorContext Initialize(
        Guid processId);

    ProcessorContext Reconcile(
        ProcessorContext context);

    ProcessorContext ApplyExecution(
        ProcessorContext context,
        StepCandidate candidate,
        ExecutionDecision decision);

    ProcessorContext ApplyUnfinishedStep(
        ProcessorContext context,
        StepCandidate candidate,
        StepExecutionStatus status);
}

internal sealed class ProcessorStateUpdater(
    IProcessorStepRegistry registry,
    KaleidoServiceOptions serviceOptions)
    : IProcessorStateUpdater
{

    public ProcessorContext Initialize(
        Guid processId)
    {
        return new ProcessorContext
        {
            ProcessId = processId,

            ProcessorName =
                serviceOptions.ServiceName,

            State = ProcessExecutionState.Active,

            CreatedUtc = DateTimeOffset.UtcNow,

            UpdatedUtc = DateTimeOffset.UtcNow,

            Steps =
                registry
                    .Registrations
                    .Select(
                        registration =>
                            new StepContext
                            {
                                StepName =
                                    registration.Metadata.Name,

                                Status =
                                    StepExecutionStatus.Pending,
                            })
                    .ToArray(),
        };
    }

    public ProcessorContext Reconcile(
        ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(
            context);

        var steps =
            context.Steps.ToList();

        var indexByName =
            new Dictionary<string, int>(
                steps.Count,
                StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < steps.Count; i++)
        {
            indexByName[steps[i].StepName] = i;
        }

        // A step deployed since the process started appears, pending; a removed one stays in its
        // history (ADR 0026, ADR 0029).
        foreach (var registration in registry.Registrations)
        {
            if (indexByName.TryAdd(registration.Metadata.Name, steps.Count))
            {
                steps.Add(
                    new StepContext
                    {
                        StepName =
                            registration.Metadata.Name,

                        Status =
                            StepExecutionStatus.Pending,
                    });
            }
        }

        return context with
        {
            UpdatedUtc = DateTimeOffset.UtcNow,
            Steps = steps,
        };
    }

    public ProcessorContext ApplyExecution(
        ProcessorContext context,
        StepCandidate candidate,
        ExecutionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(decision);

        // A process violation is the handler's defect (an invalid next step or request), not a
        // business outcome: like an exception, only the step's status records it (#522).
        if (decision.StepStatus == StepExecutionStatus.Exception)
        {
            return ApplyUnfinishedStep(context, candidate, StepExecutionStatus.Exception);
        }

        var step =
            GetStep(
                context,
                candidate);

        // A business failure is recorded as failed, not completed.
        var updatedStep =
            Executed(
                step,
                context,
                decision.StepStatus);

        var steps =
            context.Steps.ToList();

        ReplaceStep(
            steps,
            updatedStep);

        return context with
        {
            State =
                MapState(
                    decision),

            RequiredStep =
                decision.RequiredStep,

            RequiredInformationRequest =
                decision.InformationRequest,

            TargetProcessorName =
                decision.TargetProcessorName,

            AvailableSteps =
                decision.AvailableSteps,

            UpdatedUtc = DateTimeOffset.UtcNow,

            Steps =
                steps,
        };
    }

    // A step whose attempt did not finish with a handler result that changes the process: an
    // unexpected error, a process violation, a validation failure or a cancellation. Each is the
    // caller's or the handler's problem, not a business outcome, so the process keeps the state,
    // required step and available steps it had before the step, and the step can be sent again
    // (ADR 0008). Only the step's status records what happened (#536).
    public ProcessorContext ApplyUnfinishedStep(
        ProcessorContext context,
        StepCandidate candidate,
        StepExecutionStatus status)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidate);

        if (status is not (StepExecutionStatus.Exception or StepExecutionStatus.ValidationFailed or StepExecutionStatus.Canceled))
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"'{status}' is not the status of an unfinished step.");
        }

        var steps =
            context.Steps.ToList();

        ReplaceStep(
            steps,
            Executed(
                GetStep(
                    context,
                    candidate),
                context,
                status));

        return context with
        {
            UpdatedUtc = DateTimeOffset.UtcNow,

            Steps = steps,
        };
    }
    // An execution attempt of the step in this request: its status, the request and caller that ran it
    // (from the context, stamped per request by the runtime), and when. The first attempt's time and caller are kept.
    private static StepContext Executed(
        StepContext step,
        ProcessorContext context,
        StepExecutionStatus status)
    {
        var now = DateTimeOffset.UtcNow;

        return step with
        {
            Status = status,
            LatestRequestId = context.LatestRequestId,
            FirstExecuted = step.FirstExecuted ?? now,
            StartedBy = step.FirstExecuted is null ? context.UpdatedBy : step.StartedBy,
            LastExecuted = now,
            LastUpdatedBy = context.UpdatedBy,
        };
    }

    private static StepContext GetStep(
        ProcessorContext context,
        StepCandidate candidate)
    {
        return context.FindStep(
            candidate.StepName)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Step '{candidate.StepName}' was not found in processor state.");
    }

    private static void ReplaceStep(
        List<StepContext> steps,
        StepContext updated)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            if (string.Equals(
                    steps[index].StepName,
                    updated.StepName,
                    StringComparison.OrdinalIgnoreCase))
            {
                steps[index] = updated;
                return;
            }
        }

        throw new KaleidoFrameworkException(
            FrameworkErrorCodes.MissingRegistration,
            $"Step '{updated.StepName}' was not found in processor state.");
    }

    private static ProcessExecutionState MapState(
        ExecutionDecision decision)
    {
        return decision.Type switch
        {
            ExecutionDecisionType.Continue =>
                ProcessExecutionState.Active,

            ExecutionDecisionType.Complete =>
                ProcessExecutionState.Complete,

            ExecutionDecisionType.BusinessFailure =>
                ProcessExecutionState.BusinessFailure,

            ExecutionDecisionType.AwaitingRequiredStep =>
                ProcessExecutionState.AwaitingRequiredStep,

            ExecutionDecisionType.AwaitingStepSelection =>
                ProcessExecutionState.AwaitingStepSelection,

            ExecutionDecisionType.AwaitingInformation =>
                ProcessExecutionState.AwaitingInformation,

            ExecutionDecisionType.HandOff =>
                ProcessExecutionState.HandOff,

            _ => throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Unsupported execution decision '{decision.Type}'."),
        };
    }
}

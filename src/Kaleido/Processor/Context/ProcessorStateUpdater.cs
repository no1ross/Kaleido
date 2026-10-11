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

    ProcessorContext ApplyException(
        ProcessorContext context,
        StepCandidate candidate);

    ProcessorContext ApplyCancellation(
        ProcessorContext context,
        StepCandidate candidate);
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

                                Version =
                                    registration.Metadata.Version,

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

        foreach (var registration in registry.Registrations)
        {
            if (!indexByName.TryGetValue(
                    registration.Metadata.Name,
                    out var index))
            {
                indexByName[registration.Metadata.Name] = steps.Count;

                steps.Add(
                    new StepContext
                    {
                        StepName =
                            registration.Metadata.Name,

                        Version =
                            registration.Metadata.Version,

                        Status =
                            StepExecutionStatus.Pending,
                    });

                continue;
            }

            steps[index] =
                steps[index] with
                {
                    Version =
                        registration.Metadata.Version,
                };
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

        var step =
            GetStep(
                context,
                candidate);

        // A business failure ends the process; the failed step is recorded as failed, not completed.
        var updatedStep =
            step with
            {
                Status =
                    decision.Type == ExecutionDecisionType.BusinessFailure
                        ? StepExecutionStatus.Failed
                        : StepExecutionStatus.Completed,

                LatestRequestId =
                    context.LatestRequestId,

                LastExecuted =
                    DateTimeOffset.UtcNow,
            };

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

    public ProcessorContext ApplyException(
        ProcessorContext context,
        StepCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidate);

        var step =
            GetStep(
                context,
                candidate);

        var updatedStep =
            step with
            {
                Status =
                    StepExecutionStatus.Exception,

                LatestRequestId =
                    context.LatestRequestId,

                LastExecuted =
                    DateTimeOffset.UtcNow,
            };

        var steps =
            context.Steps.ToList();

        ReplaceStep(
            steps,
            updatedStep);

        // An unexpected error is a defect, not a business outcome: the process keeps the state,
        // required step and available steps it had before the step, so the step can be retried
        // once the defect is fixed. Only the step's status records what happened.
        return context with
        {
            UpdatedUtc = DateTimeOffset.UtcNow,

            Steps = steps,
        };
    }

    public ProcessorContext ApplyCancellation(
        ProcessorContext context,
        StepCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidate);

        var step =
            GetStep(
                context,
                candidate);

        var updatedStep =
            step with
            {
                Status =
                    StepExecutionStatus.Canceled,

                LatestRequestId =
                    context.LatestRequestId,

                LastExecuted =
                    DateTimeOffset.UtcNow,
            };

        var steps =
            context.Steps.ToList();

        ReplaceStep(
            steps,
            updatedStep);

        return context with
        {
            State =
                ProcessExecutionState.Canceled,

            RequiredStep = null,

            RequiredInformationRequest = null,

            TargetProcessorName = null,

            AvailableSteps = [],

            Steps = steps,
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
        IList<StepContext> steps,
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

            ExecutionDecisionType.ProcessViolation =>
                ProcessExecutionState.ProcessViolation,

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

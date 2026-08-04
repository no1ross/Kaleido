using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Process.Participant.Context;

internal interface IProcessStateUpdater
{
    ParticipantContext Initialize(
        string correlationId);

    ParticipantContext Reconcile(
        ParticipantContext context);

    ParticipantContext ApplyExecution(
        ParticipantContext context,
        StepCandidate candidate,
        ExecutionDecision decision);

    ParticipantContext ApplyException(
        ParticipantContext context,
        StepCandidate candidate);

    ParticipantContext ApplyCancellation(
        ParticipantContext context,
        StepCandidate candidate);
}

internal sealed class ProcessStateUpdater : IProcessStateUpdater
{
    private readonly IProcessStepRegistry _registry;

    public ProcessStateUpdater(
        IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public ParticipantContext Initialize(
        string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            correlationId);

        return new ParticipantContext
        {
            ParticipantProcessId = correlationId,

            State = ProcessExecutionState.Active,

            Steps =
                _registry
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
                                    StepExecutionStatus.Pending
                            })
                    .ToArray()
        };
    }

    public ParticipantContext Reconcile(
        ParticipantContext context)
    {
        ArgumentNullException.ThrowIfNull(
            context);

        var steps =
            context.Steps.ToList();

        foreach (var registration in _registry.Registrations)
        {
            var existing =
                steps.FirstOrDefault(
                    x => string.Equals(
                        x.StepName,
                        registration.Metadata.Name,
                        StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                steps.Add(
                    new StepContext
                    {
                        StepName =
                            registration.Metadata.Name,

                        Version =
                            registration.Metadata.Version,

                        Status =
                            StepExecutionStatus.Pending
                    });

                continue;
            }

            var updated =
                existing with
                {
                    Version =
                        registration.Metadata.Version
                };

            ReplaceStep(
                steps,
                updated);
        }

        return context with
        {
            Steps = steps
        };
    }

    public ParticipantContext ApplyExecution(
        ParticipantContext context,
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

        var updatedStep =
            step with
            {
                Status =
                    StepExecutionStatus.Completed,

                LastRequestId =
                    context.LastestRequestId,

                LastExecuted =
                    DateTimeOffset.UtcNow
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

            AvailableSteps =
                decision.AvailableSteps,

            Steps =
                steps
        };
    }

    public ParticipantContext ApplyException(
        ParticipantContext context,
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

                LastRequestId =
                    context.LastestRequestId,

                LastExecuted =
                    DateTimeOffset.UtcNow
            };

        var steps =
            context.Steps.ToList();

        ReplaceStep(
            steps,
            updatedStep);

        return context with
        {
            State =
                ProcessExecutionState.Exception,

            RequiredStep = null,

            AvailableSteps = [],

            Steps = steps
        };
    }

    public ParticipantContext ApplyCancellation(
        ParticipantContext context,
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

                LastRequestId =
                    context.LastestRequestId,

                LastExecuted =
                    DateTimeOffset.UtcNow
            };

        var steps =
            context.Steps.ToList();

        ReplaceStep(
            steps,
            updatedStep);

        return context with
        {
            State =
                ProcessExecutionState.Cancelled,

            RequiredStep = null,

            AvailableSteps = [],

            Steps = steps
        };
    }

    private static StepContext GetStep(
        ParticipantContext context,
        StepCandidate candidate)
    {
        return context.FindStep(
            candidate.StepName)
            ?? throw new InvalidOperationException(
                $"Step '{candidate.StepName}' was not found in participant state.");
    }

    private static void ReplaceStep(
        IList<StepContext> steps,
        StepContext updated)
    {
        var index =
            steps
                .Select(
                    (step, index) => new
                    {
                        step,
                        index
                    })
                .First(
                    x => string.Equals(
                        x.step.StepName,
                        updated.StepName,
                        StringComparison.OrdinalIgnoreCase))
                .index;

        steps[index] = updated;
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

            _ => throw new InvalidOperationException(
                $"Unsupported execution decision '{decision.Type}'.")
        };
    }
}
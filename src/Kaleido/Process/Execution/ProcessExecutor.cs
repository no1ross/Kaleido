using Kaleido.Process.Context;
using Kaleido.Process.Eventing;
using Kaleido.Process.Observability;
using Kaleido.Process.Planning;

namespace Kaleido.Process.Execution;

public interface IExecutionProcessor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context,
        ProcessorRequest originalRequest,
        CancellationToken cancellationToken = default);
}

internal sealed class ExecutionProcessor(
    IProcessStepInvoker invoker,
    IStepExecutionEvaluator evaluator,
    IProcessStateUpdater stateUpdater,
    IProcessContextStore stateRepository,
    IStepAvailabilityResolver availabilityResolver,
    IProcessEventFactory eventFactory,
    IEventPublisher eventPublisher,
    IProcessObservability observability,
    IKaleidoCorrelationContextAccessor correlationAccessor)
    : IExecutionProcessor
{

    public async Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context,
        ProcessorRequest originalRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        if (candidates.Count == 0)
        {
            return new ProcessExecutionResult
            {
                ProcessId =
                    context.ProcessId,

                State =
                    context.State,

                RequiredStep =
                    context.RequiredStep,

                TargetProcessorName =
                    context.TargetProcessorName,

                AvailableSteps =
                    context.AvailableSteps,

                Outcomes =
                    []
            };
        }

        var outcomes =
            new List<ProcessExecutionOutcome>();

        var remainingCandidates =
            candidates.ToList();

        var currentCandidate =
            remainingCandidates.FirstOrDefault();

        while (currentCandidate is not null)
        {
            var candidate =
                currentCandidate;

            //
            // Important:
            // This candidate has now been selected for execution in
            // this request. Even if the step is repeatable, this specific
            // submitted candidate should not be selected again.
            //
            remainingCandidates.Remove(
                candidate);

            using var stepObservation =
                observability.BeginStep(
                    new ProcessStepObservationDetails(
                        candidate.StepName,
                        candidate.Registration?.Metadata.Version));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stepContext =
                    context.FindStep(
                        candidate.StepName)
                    ?? throw new KaleidoFrameworkException(
                        FrameworkErrorCodes.MissingRegistration,
                        $"Step '{candidate.StepName}' was not found in processor state.");

                var initialAvailableSteps =
                    availabilityResolver.Resolve(
                        candidate,
                        candidates,
                        context);

                var processStepContext =
                    new ProcessStepContext(
                        context.ProcessId,
                        stepContext,
                        initialAvailableSteps,
                        originalRequest);

                var result =
                    await invoker.ExecuteAsync(
                        candidate.Registration
                        ?? throw new KaleidoFrameworkException(
                            FrameworkErrorCodes.MissingRegistration,
                            $"Step '{candidate.StepName}' does not contain registration metadata."),
                        candidate.Step
                        ?? throw new KaleidoFrameworkException(
                            FrameworkErrorCodes.MissingRegistration,
                            $"Step '{candidate.StepName}' does not contain a step instance."),
                        processStepContext,
                        cancellationToken);

                var decision =
                    evaluator.Evaluate(
                        candidate,
                        result,
                        remainingCandidates,
                        context);

                context =
                    stateUpdater.ApplyExecution(
                        context,
                        candidate,
                        decision);

                await stateRepository.SaveAsync(
                    context,
                    cancellationToken);

                var executionStatus = MapStatus(candidate, result, decision);

                stepObservation.DecisionRecorded(
                    decision.Type.ToString(),
                    executionStatus.ToString());

                var outcome =
                    CreateOutcome(
                        candidate,
                        result,
                        decision,
                        executionStatus);

                outcomes.Add(
                    outcome);

                await eventPublisher.PublishAsync(
                    eventFactory.CreateStepCompleted(
                        correlationAccessor.Current,
                        context,
                        candidate,
                        outcome,
                        result),
                    cancellationToken);

                currentCandidate =
                    GetNextCandidate(
                        decision);
            }
            catch (OperationCanceledException)
            {
                stepObservation.Canceled();

                context =
                    stateUpdater.ApplyCancellation(
                        context,
                        candidate);

                await stateRepository.SaveAsync(
                    context,
                    CancellationToken.None);

                outcomes.Add(
                    new ProcessExecutionOutcome
                    {
                        StepName =
                            candidate.StepName,

                        Status =
                            StepExecutionStatus.Canceled,

                        Outcome =
                            GetStepOutcome(StepExecutionStatus.Canceled),

                        Decision =
                            ExecutionDecisionType.ProcessViolation,

                        RuntimeMessages =
                        [
                            StepProcessingMessage.Error(
                            StepProcessingMessageCode.ExecutionCancelled,
                            "Step execution was cancelled.")
                        ],

                        Response = null
                    });

                break;
            }
            catch (Exception exception)
            {
                stepObservation.StepFailed(exception);

                context =
                    stateUpdater.ApplyException(
                        context,
                        candidate);

                await stateRepository.SaveAsync(
                    context,
                    CancellationToken.None);

                outcomes.Add(
                    new ProcessExecutionOutcome
                    {
                        StepName =
                            candidate.StepName,

                        Status =
                            StepExecutionStatus.Exception,

                        Outcome =
                            GetStepOutcome(StepExecutionStatus.Exception),

                        Decision =
                            ExecutionDecisionType.ProcessViolation,

                        RuntimeMessages =
                        [
                            StepProcessingMessage.Error(
                            StepProcessingMessageCode.FrameworkException,
                            $"An unexpected error occurred while executing step '{candidate.StepName}'.")
                        ],

                        Response = null
                    });

                break;
            }
        }

        return new ProcessExecutionResult
        {
            ProcessId =
                context.ProcessId,

            State =
                context.State,

            RequiredStep =
                context.RequiredStep,

            TargetProcessorName =
                context.TargetProcessorName,

            AvailableSteps =
                context.AvailableSteps,

            Outcomes =
                outcomes
        };
    }

    private static StepCandidate? GetNextCandidate(
        ExecutionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return decision.Type == ExecutionDecisionType.Continue
            ? decision.NextCandidate
            : null;
    }

    private static StepExecutionOutcome GetStepOutcome(
        StepExecutionStatus status)
    {
        return status switch
        {
            StepExecutionStatus.Pending =>
                StepExecutionOutcome.Blocked,

            StepExecutionStatus.Completed =>
                StepExecutionOutcome.Completed,

            StepExecutionStatus.ValidationFailed =>
                StepExecutionOutcome.Failed,

            StepExecutionStatus.Exception =>
                StepExecutionOutcome.Failed,

            StepExecutionStatus.Skipped =>
                StepExecutionOutcome.Blocked,

            StepExecutionStatus.Canceled =>
                StepExecutionOutcome.Blocked,

            _ =>
                StepExecutionOutcome.Pending
        };
    }

    private static ProcessExecutionOutcome CreateOutcome(
        StepCandidate candidate,
        ProcessStepInvokerResult result,
        ExecutionDecision decision,
        StepExecutionStatus executionStatus)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(decision);

        return new ProcessExecutionOutcome
        {
            StepName =
                candidate.StepName,

            Status =
                executionStatus,

            Outcome =
                GetStepOutcome(executionStatus),

            Decision =
                decision.Type,

            RuntimeMessages =
                decision.Messages,

            BusinessMessages = result.Messages,

            Response = result.Response
        };
    }

    private static StepExecutionStatus MapStatus(
        StepCandidate candidate,
        ProcessStepInvokerResult result,
        ExecutionDecision decision)
    {
        if (candidate.Status == StepCandidateStatus.Invalid)
        {
            return StepExecutionStatus.ValidationFailed;
        }

        if (candidate.Status == StepCandidateStatus.Satisfied)
        {
            return StepExecutionStatus.Skipped;
        }

        if (candidate.Status == StepCandidateStatus.Pending)
        {
            return StepExecutionStatus.Pending;
        }

        if (decision.Type == ExecutionDecisionType.ProcessViolation)
        {
            return StepExecutionStatus.Exception;
        }

        return result.Succeeded
            ? StepExecutionStatus.Completed
            : StepExecutionStatus.Exception;
    }

}
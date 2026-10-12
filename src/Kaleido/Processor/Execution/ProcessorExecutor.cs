using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;
using Microsoft.Extensions.Logging;

namespace Kaleido.Processor.Execution;

internal interface IProcessorExecutor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context,
        ProcessorRequest originalRequest,
        CancellationToken cancellationToken = default);
}

internal sealed partial class ProcessorExecutor(
    IProcessStepInvoker invoker,
    IStepExecutionEvaluator evaluator,
    IProcessorStateUpdater stateUpdater,
    IProcessorContextStore stateRepository,
    IStepAvailabilityResolver availabilityResolver,
    IProcessorEventFactory eventFactory,
    IEventPublisher eventPublisher,
    IProcessorObservability observability,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    ILogger<ProcessorExecutor> logger)
    : IProcessorExecutor
{

    public async Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context,
        ProcessorRequest originalRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        var outcomes = new List<ProcessExecutionOutcome>();
        var remainingCandidates = candidates.ToList();
        var currentCandidate = remainingCandidates.FirstOrDefault();

        while (currentCandidate is not null)
        {
            var candidate = currentCandidate;

            //
            // Important:
            // This candidate has now been selected for execution in
            // this request. Even if the step is repeatable, this specific
            // submitted candidate should not be selected again.
            //
            remainingCandidates.Remove(candidate);

            (context, currentCandidate) =
                await RunAsync(candidate, candidates, remainingCandidates, context, originalRequest, outcomes, cancellationToken);
        }

        return ToResult(context, outcomes);
    }

    // Runs one candidate and records its outcome. Returns the process state afterwards and the
    // candidate to run next (null when this request stops here).
    private async Task<(ProcessorContext Context, StepCandidate? Next)> RunAsync(
        StepCandidate candidate,
        IReadOnlyCollection<StepCandidate> candidates,
        List<StepCandidate> remainingCandidates,
        ProcessorContext context,
        ProcessorRequest originalRequest,
        List<ProcessExecutionOutcome> outcomes,
        CancellationToken cancellationToken)
    {
        using var stepObservation = BeginStep(candidate);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (result, decision) =
                await InvokeAsync(candidate, candidates, remainingCandidates, context, originalRequest, cancellationToken);

            // A rejection leaves the process exactly as it was: nothing is saved, the step is
            // not completed, and no later candidate of this request runs.
            if (decision.Type == ExecutionDecisionType.Rejected)
            {
                stepObservation.DecisionRecorded(decision.Type.ToString(), nameof(StepExecutionStatus.Rejected));
                outcomes.Add(CreateOutcome(candidate, result, decision, StepExecutionStatus.Rejected));

                return (context, null);
            }

            context = stateUpdater.ApplyExecution(context, candidate, decision);
            await stateRepository.SaveAsync(context, cancellationToken);
            outcomes.Add(Complete(candidate, result, decision, context, stepObservation, cancellationToken));

            return (context, GetNextCandidate(decision));
        }
        catch (OperationCanceledException)
        {
            stepObservation.Canceled();

            return (await FailAsync(
                stateUpdater.ApplyCancellation(context, candidate),
                outcomes,
                FailedOutcome(candidate, StepExecutionStatus.Canceled, ProcessorErrorCodes.ExecutionCanceled, "Step execution was cancelled.")), null);
        }
        catch (KaleidoValidationException exception)
        {
            stepObservation.StepFailed(exception);

            return (await FailAsync(
                stateUpdater.ApplyException(context, candidate),
                outcomes,
                FailedOutcome(candidate, StepExecutionStatus.ValidationFailed, exception.Code, exception.Message)), null);
        }
        // Any other failure of the step (cancellation and validation are handled above).
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stepObservation.StepFailed(exception);

            return (await FailAsync(
                stateUpdater.ApplyException(context, candidate),
                outcomes,
                FailedOutcome(
                    candidate,
                    StepExecutionStatus.Exception,
                    ProcessorErrorCodes.FrameworkException,
                    $"An unexpected error occurred while executing step '{candidate.StepName}'.")), null);
        }
    }
    private IProcessorStepObservation BeginStep(StepCandidate candidate) =>
        observability.BeginStep(
            new ProcessorStepObservationDetails(
                candidate.StepName,
                candidate.Registration?.Metadata.Version,
                candidate.Registration?.IsInformationStep == true));

    // Invokes the step's handler and evaluates its result against the process rules.
    private async Task<(StepInvocationResult Result, ExecutionDecision Decision)> InvokeAsync(
        StepCandidate candidate,
        IReadOnlyCollection<StepCandidate> candidates,
        IReadOnlyCollection<StepCandidate> remainingCandidates,
        ProcessorContext context,
        ProcessorRequest originalRequest,
        CancellationToken cancellationToken)
    {
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

        return (result, decision);
    }

    // A saved step: records the decision, builds the outcome and publishes the step-completed event
    // (best-effort - the state is already saved).
    private ProcessExecutionOutcome Complete(
        StepCandidate candidate,
        StepInvocationResult result,
        ExecutionDecision decision,
        ProcessorContext context,
        IProcessorStepObservation stepObservation,
        CancellationToken cancellationToken)
    {
        var executionStatus = MapStatus(candidate, result, decision);

        stepObservation.DecisionRecorded(
            decision.Type.ToString(),
            executionStatus.ToString());

        if (decision.InformationRequest is { } informationRequest)
        {
            stepObservation.InformationRequested(
                informationRequest.InformationRequestId);
        }

        var outcome =
            CreateOutcome(
                candidate,
                result,
                decision,
                executionStatus);

        try
        {
            _ = eventPublisher.PublishAsync(
                eventFactory.CreateStepCompleted(
                    correlationAccessor.Current,
                    context,
                    candidate,
                    outcome,
                    result),
                cancellationToken);
        }
        catch (Exception publishException) when (publishException is not OperationCanceledException)
        {
            LogStepEventPublishFailed(logger, publishException, candidate.StepName, context.ProcessId);
        }

        return outcome;
    }

    // A failed step's state is saved even when the request was cancelled.
    private async Task<ProcessorContext> FailAsync(
        ProcessorContext context,
        List<ProcessExecutionOutcome> outcomes,
        ProcessExecutionOutcome outcome)
    {
        await stateRepository.SaveAsync(
            context,
            CancellationToken.None);

        outcomes.Add(outcome);

        return context;
    }
    private static ProcessExecutionOutcome FailedOutcome(
        StepCandidate candidate,
        StepExecutionStatus status,
        string code,
        string message) =>
        new()
        {
            StepName =
                candidate.StepName,

            Status =
                status,

            Outcome =
                GetStepOutcome(status),

            Decision =
                ExecutionDecisionType.ProcessViolation,

            RuntimeMessages =
            [
                StepProcessingMessage.Error(
                    code,
                    message),
            ],

            Response = null,
        };

    private static ProcessExecutionResult ToResult(
        ProcessorContext context,
        List<ProcessExecutionOutcome> outcomes) =>
        new()
        {
            ProcessId =
                context.ProcessId,

            State =
                context.State,

            RequiredStep =
                context.RequiredStep,

            RequiredInformationRequest =
                context.RequiredInformationRequest,

            TargetProcessorName =
                context.TargetProcessorName,

            AvailableSteps =
                context.AvailableSteps,

            Outcomes =
                outcomes,
        };

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

            StepExecutionStatus.Failed =>
                StepExecutionOutcome.Failed,

            StepExecutionStatus.Rejected =>
                StepExecutionOutcome.Rejected,

            _ =>
                StepExecutionOutcome.Pending,
        };
    }

    private static ProcessExecutionOutcome CreateOutcome(
        StepCandidate candidate,
        StepInvocationResult result,
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

            Response = result.Response,
        };
    }

    private static StepExecutionStatus MapStatus(
        StepCandidate candidate,
        StepInvocationResult result,
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
            : StepExecutionStatus.Failed;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Event publish failed for step {StepName} on process {ProcessId}. State was saved; event delivery is best-effort.")]
    private static partial void LogStepEventPublishFailed(ILogger logger, Exception exception, string stepName, Guid processId);
}

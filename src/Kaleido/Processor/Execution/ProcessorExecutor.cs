using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;

namespace Kaleido.Processor.Execution;

internal interface IProcessorExecutor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context,
        ProcessorRequest originalRequest,
        CancellationToken cancellationToken = default);
}

internal sealed class ProcessorExecutor(
    IProcessStepInvoker invoker,
    IStepExecutionEvaluator evaluator,
    IProcessorStateUpdater stateUpdater,
    IKaleidoContextStore stateRepository,
    IStepAvailabilityResolver availabilityResolver,
    IProcessorEventFactory eventFactory,
    IKaleidoEventOutbox eventOutbox,
    IProcessorObservability observability,
    IKaleidoCorrelationContextAccessor correlationAccessor)
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

        var run = new ExecutionRun(candidates, originalRequest);
        var currentCandidate = run.RemainingCandidates.FirstOrDefault();

        while (currentCandidate is not null)
        {
            var candidate = currentCandidate;

            //
            // Important:
            // This candidate has now been selected for execution in
            // this request. Even if the step is repeatable, this specific
            // submitted candidate should not be selected again.
            //
            run.RemainingCandidates.Remove(candidate);

            (context, currentCandidate) =
                await RunAsync(candidate, run, context, cancellationToken);
        }

        return ToResult(context, run.Outcomes);
    }

    // Runs one candidate and records its outcome. Returns the process state afterwards and the
    // candidate to run next (null when this request stops here).
    private async Task<(ProcessorContext Context, StepCandidate? Next)> RunAsync(
        StepCandidate candidate,
        ExecutionRun run,
        ProcessorContext context,
        CancellationToken cancellationToken)
    {
        using var stepObservation = BeginStep(candidate);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (result, decision) =
                await InvokeAsync(candidate, run, context, cancellationToken);

            // A rejection leaves the process exactly as it was: nothing is saved, the step is
            // not completed, and no later candidate of this request runs.
            if (decision.Type == ExecutionDecisionType.Rejected)
            {
                stepObservation.DecisionRecorded(decision.Type.ToString(), nameof(StepExecutionStatus.Rejected));
                run.Outcomes.Add(CreateOutcome(candidate, result, decision, StepExecutionStatus.Rejected));

                return (context, null);
            }

            // The handler has returned: its outcome is final. Saving it (state and step-completed
            // event together) is not cancellable, so a cancellation that arrives now neither discards
            // the completion nor makes a retry run the handler's side effects again.
            context = await SaveStepAsync(candidate, result, decision, run, context, stepObservation);

            return (context, GetNextCandidate(decision));
        }
        catch (OperationCanceledException)
        {
            stepObservation.Canceled();

            return (await FailAsync(
                stateUpdater.ApplyCancellation(context, candidate),
                run,
                FailedOutcome(candidate, StepExecutionStatus.Canceled, ProcessorErrorCodes.ExecutionCanceled, "Step execution was cancelled.")), null);
        }
        catch (KaleidoValidationException exception)
        {
            stepObservation.StepValidationFailed(exception.Code);

            return (await FailAsync(
                stateUpdater.ApplyException(context, candidate),
                run,
                FailedOutcome(candidate, StepExecutionStatus.ValidationFailed, exception.Code, exception.Message)), null);
        }
        // Any other failure of the step (cancellation and validation are handled above). A concurrency
        // conflict is not a step failure: nothing was saved, and the caller rereads the process (409).
        catch (Exception exception) when (exception is not OperationCanceledException and not KaleidoConcurrencyException)
        {
            stepObservation.StepFailed(exception);

            return (await FailAsync(
                stateUpdater.ApplyException(context, candidate),
                run,
                FailedOutcome(
                    candidate,
                    StepExecutionStatus.Exception,
                    ProcessorErrorCodes.FrameworkException,
                    $"An unexpected error occurred while executing step '{candidate.StepName}'.")), null);
        }
    }

    // A step that ran: applies its decision, records its outcome, and saves the state together with
    // the step-completed event - both or neither, without the request's cancellation token.
    private async Task<ProcessorContext> SaveStepAsync(
        StepCandidate candidate,
        StepInvocationResult result,
        ExecutionDecision decision,
        ExecutionRun run,
        ProcessorContext context,
        IProcessorStepObservation stepObservation)
    {
        context = stateUpdater.ApplyExecution(context, candidate, decision);

        var outcome = Complete(candidate, result, decision, stepObservation);
        var stepCompleted = eventFactory.CreateStepCompleted(correlationAccessor.Current, context, candidate, outcome, result);

        context = await SaveAsync(context, eventOutbox.Prepare(stepCompleted), CancellationToken.None);
        run.Outcomes.Add(outcome);

        return context;
    }

    // Saves the state, with the step-completed event when there is one.
    private async Task<ProcessorContext> SaveAsync(
        ProcessorContext context,
        KaleidoOutboxEvent? stepEvent,
        CancellationToken cancellationToken)
    {
        var saved = await stateRepository.SaveAsync(context, stepEvent is null ? [] : [stepEvent], cancellationToken);

        if (stepEvent is not null)
        {
            eventOutbox.Saved();
        }

        return saved;
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
        ExecutionRun run,
        ProcessorContext context,
        CancellationToken cancellationToken)
    {
        var candidates = run.Candidates;
        var remainingCandidates = run.RemainingCandidates;
        var originalRequest = run.OriginalRequest;

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

    // A step about to be saved: records the decision and builds the outcome.
    private static ProcessExecutionOutcome Complete(
        StepCandidate candidate,
        StepInvocationResult result,
        ExecutionDecision decision,
        IProcessorStepObservation stepObservation)
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

        return CreateOutcome(
            candidate,
            result,
            decision,
            executionStatus);
    }

    // A failed step's state is saved even when the request was cancelled.
    private async Task<ProcessorContext> FailAsync(
        ProcessorContext context,
        ExecutionRun run,
        ProcessExecutionOutcome outcome)
    {
        var saved = await SaveAsync(
            context,
            stepEvent: null,
            CancellationToken.None);

        run.Outcomes.Add(outcome);

        return saved;
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
                StepExecutionOutcome.Canceled,

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

    // The state of one request's execution.
    private sealed class ExecutionRun(
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorRequest originalRequest)
    {
        public IReadOnlyCollection<StepCandidate> Candidates { get; } = candidates;

        public List<StepCandidate> RemainingCandidates { get; } = [.. candidates];

        public ProcessorRequest OriginalRequest { get; } = originalRequest;

        public List<ProcessExecutionOutcome> Outcomes { get; } = [];
    }
}

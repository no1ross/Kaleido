using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;
using Kaleido.Processor.Recovery;

namespace Kaleido.Processor.Execution;

internal interface IProcessorExecutor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context,
        CancellationToken cancellationToken);
}

internal sealed class ProcessorExecutor(
    IProcessStepInvoker invoker,
    IStepExecutionEvaluator evaluator,
    IProcessorStateUpdater stateUpdater,
    IProcessorTransition transition,
    IStepAvailabilityResolver availabilityResolver,
    IProcessorEventFactory eventFactory,
    IKaleidoEventWriter eventWriter,
    IProcessorObservability observability,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IProcessorOutcomeRecovery outcomeRecovery)
    : IProcessorExecutor
{

    public async Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        var run = new ExecutionRun(candidates);
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
        (StepInvocationResult Result, ExecutionDecision Decision) invoked;

        // The handler phase only: what fails here is the step; a failed save is not (#537, SaveStepAsync).
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            invoked = await InvokeAsync(candidate, run, context, cancellationToken);
        }
        // Only the caller's token is a cancellation (#517); any other OperationCanceledException fails below.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stepObservation.Canceled();

            return (await FailAsync(
                stateUpdater.ApplyUnfinishedStep(context, candidate, StepExecutionStatus.Canceled),
                run,
                FailedOutcome(candidate, StepExecutionStatus.Canceled, ProcessorErrorCodes.ExecutionCanceled, "Step execution was cancelled.")), null);
        }
        catch (KaleidoValidationException exception)
        {
            stepObservation.StepValidationFailed(exception.Code);

            return (await FailAsync(
                stateUpdater.ApplyUnfinishedStep(context, candidate, StepExecutionStatus.ValidationFailed),
                run,
                FailedOutcome(candidate, StepExecutionStatus.ValidationFailed, exception.Code, exception.Message)), null);
        }
        // Any other failure (a concurrency conflict is not one: nothing was saved, the caller rereads, 409).
        catch (Exception exception) when (exception is not KaleidoConcurrencyException)
        {
            stepObservation.StepFailed(exception);

            return (await FailAsync(
                stateUpdater.ApplyUnfinishedStep(context, candidate, StepExecutionStatus.Exception),
                run,
                FailedOutcome(
                    candidate,
                    StepExecutionStatus.Exception,
                    ProcessorErrorCodes.FrameworkException,
                    $"An unexpected error occurred while executing step '{candidate.StepName}'.")), null);
        }

        return await FinishAsync(candidate, invoked.Result, invoked.Decision, run, context, stepObservation);
    }

    // The handler has returned: a rejection leaves the process as it was (nothing is saved, and no later
    // candidate runs); any other outcome is final and saved. The save is not cancellable, so a late
    // cancellation neither discards the outcome nor makes a retry run the handler's side effects again.
    private async Task<(ProcessorContext Context, StepCandidate? Next)> FinishAsync(
        StepCandidate candidate,
        StepInvocationResult result,
        ExecutionDecision decision,
        ExecutionRun run,
        ProcessorContext context,
        IProcessorStepObservation stepObservation)
    {
        if (decision.Type == ExecutionDecisionType.Rejected)
        {
            stepObservation.DecisionRecorded(decision.Type.ToString(), nameof(StepExecutionStatus.Rejected));
            run.Outcomes.Add(CreateOutcome(candidate, result, decision, StepExecutionStatus.Rejected));

            return (context, null);
        }

        context = await SaveStepAsync(candidate, result, decision, run, context, stepObservation);

        return (context, GetNextCandidate(decision));
    }

    // A step that ran: applies its decision and saves the state together with the step-completed
    // event - both or neither, without the request's cancellation token - then records the decision.
    private async Task<ProcessorContext> SaveStepAsync(
        StepCandidate candidate,
        StepInvocationResult result,
        ExecutionDecision decision,
        ExecutionRun run,
        ProcessorContext context,
        IProcessorStepObservation stepObservation)
    {
        context = stateUpdater.ApplyExecution(context, candidate, decision);

        var outcome = CreateOutcome(candidate, result, decision, decision.StepStatus);
        var transitionEvent = eventWriter.Prepare(
            eventFactory.CreateStepCompleted(correlationAccessor.Current, context, candidate, outcome, decision.Type, result));
        var saveToken = CancellationToken.None;

        try
        {
            context = await transition.SaveAsync(context, transitionEvent, saveToken);
        }
        // The handler ran, but its outcome was not saved (#537): kept for recovery when it can be,
        // and answered so that the caller knows the step ran and does not send it again. The save is
        // not cancellable, so only its own token's cancellation would be one (#517); any other
        // OperationCanceledException is a failure.
        catch (Exception exception) when (!(exception is OperationCanceledException && saveToken.IsCancellationRequested))
        {
            var unsaved = outcomeRecovery.Keep(context, transitionEvent, candidate.StepName, exception);
            stepObservation.OutcomeUnsaved(unsaved, context, transitionEvent, exception);

            throw new StepOutcomeNotSavedException(context.ProcessId, candidate.StepName, unsaved, exception);
        }

        RecordDecision(decision, stepObservation);
        run.Outcomes.Add(outcome);

        return context;
    }

    private IProcessorStepObservation BeginStep(StepCandidate candidate) =>
        observability.BeginStep(
            new ProcessorStepObservationDetails(
                candidate.StepName,
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
                initialAvailableSteps);

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

    // A step whose outcome was saved: records its decision (never before the save, #537).
    private static void RecordDecision(
        ExecutionDecision decision,
        IProcessorStepObservation stepObservation)
    {
        stepObservation.DecisionRecorded(
            decision.Type.ToString(),
            decision.StepStatus.ToString());

        if (decision.InformationRequest is { } informationRequest)
        {
            stepObservation.InformationRequested(
                informationRequest.InformationRequestId);
        }
    }

    // A failed step's state is saved even when the request was cancelled.
    private async Task<ProcessorContext> FailAsync(
        ProcessorContext context,
        ExecutionRun run,
        ProcessExecutionOutcome outcome)
    {
        var saved = await transition.SaveAsync(
            context,
            transitionEvent: null,
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

            RuntimeMessages =
                decision.Messages,

            BusinessMessages = result.Messages,

            Response = result.Response,
        };
    }

    // The state of one request's execution.
    private sealed class ExecutionRun(
        IReadOnlyCollection<StepCandidate> candidates)
    {
        public IReadOnlyCollection<StepCandidate> Candidates { get; } = candidates;

        public List<StepCandidate> RemainingCandidates { get; } = [.. candidates];

        public List<ProcessExecutionOutcome> Outcomes { get; } = [];
    }
}

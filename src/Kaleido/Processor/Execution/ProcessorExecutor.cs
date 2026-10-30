using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;

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
    IKaleidoCorrelationContextAccessor correlationAccessor)
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
        var stepCompleted = eventFactory.CreateStepCompleted(correlationAccessor.Current, context, candidate, outcome, decision.Type, result);

        context = await transition.SaveAsync(context, eventWriter.Prepare(stepCompleted), CancellationToken.None);
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

    // A step about to be saved: records the decision and builds the outcome.
    private static ProcessExecutionOutcome Complete(
        StepCandidate candidate,
        StepInvocationResult result,
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

        return CreateOutcome(
            candidate,
            result,
            decision,
            decision.StepStatus);
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

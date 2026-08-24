using Kaleido.Eventing;
using Kaleido.Process.Eventing;
using Kaleido.Process.Observability;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.Participant.Execution;

internal sealed class ExecutionProcessor : IExecutionProcessor
{
    private readonly IProcessStepInvoker _invoker;
    private readonly IStepExecutionEvaluator _evaluator;
    private readonly IProcessStateUpdater _stateUpdater;
    private readonly IProcessContextStore _stateRepository;
    private readonly IProcessStepRegistry _stepRegistry;
    private readonly IStepAvailabilityResolver _availabilityResolver;
    private readonly IProcessEventFactory _eventFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly IProcessObservability _observability;

    public ExecutionProcessor(
        IProcessStepInvoker invoker,
        IStepExecutionEvaluator evaluator,
        IProcessStateUpdater stateUpdater,
        IProcessContextStore stateRepository,
        IProcessStepRegistry stepRegistry,
        IStepAvailabilityResolver availabilityResolver,
        IProcessEventFactory eventFactory,
        IEventPublisher eventPublisher,
        IProcessObservability observability)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(stateUpdater);
        ArgumentNullException.ThrowIfNull(stateRepository);
        ArgumentNullException.ThrowIfNull(stepRegistry);
        ArgumentNullException.ThrowIfNull(availabilityResolver);
        ArgumentNullException.ThrowIfNull(eventFactory);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(observability);

        _availabilityResolver = availabilityResolver;
        _invoker = invoker;
        _evaluator = evaluator;
        _stateUpdater = stateUpdater;
        _stateRepository = stateRepository;
        _stepRegistry = stepRegistry;
        _eventFactory = eventFactory;
        _eventPublisher = eventPublisher;
        _observability = observability;
    }

    public async Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context,
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

        while (true)
        {
            if (currentCandidate is null)
            {
                break;
            }

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
                _observability.BeginStep(
                    new ProcessStepObservationDetails(
                        candidate.StepName,
                        candidate.Registration?.Metadata.Version));

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stepContext =
                    context.FindStep(
                        candidate.StepName)
                    ?? throw new InvalidOperationException(
                        $"Step '{candidate.StepName}' was not found in participant state.");

                var initialAvailableSteps =
                    _availabilityResolver.Resolve(
                        candidate,
                        candidates,
                        context);

                var processStepContext =
                    new ProcessStepContext(
                        context.ProcessId,
                        stepContext,
                        initialAvailableSteps);

                var result =
                    await _invoker.ExecuteAsync(
                        candidate.Registration
                        ?? throw new InvalidOperationException(
                            $"Step '{candidate.StepName}' does not contain registration metadata."),
                        candidate.Step
                        ?? throw new InvalidOperationException(
                            $"Step '{candidate.StepName}' does not contain a step instance."),
                        processStepContext,
                        cancellationToken);

                var decision =
                    _evaluator.Evaluate(
                        candidate,
                        result,
                        remainingCandidates,
                        context);

                context =
                    _stateUpdater.ApplyExecution(
                        context,
                        candidate,
                        decision);

                await _stateRepository.SaveAsync(
                    context,
                    cancellationToken);

                stepObservation.DecisionRecorded(
                    decision.Type.ToString(),
                    MapStatus(
                        decision).ToString());

                var outcome =
                    CreateOutcome(
                        candidate,
                        result,
                        decision);

                outcomes.Add(
                    outcome);

                await _eventPublisher.PublishAsync(
                    _eventFactory.CreateStepCompleted(
                        context,
                        candidate,
                        outcome,
                        result,
                        GetStepOutcome(outcome.Status)),
                    cancellationToken);

                currentCandidate =
                    GetNextCandidate(
                        decision);
            }
            catch (OperationCanceledException)
            {
                stepObservation.Canceled();

                context =
                    _stateUpdater.ApplyCancellation(
                        context,
                        candidate);

                await _stateRepository.SaveAsync(
                    context,
                    CancellationToken.None);

                outcomes.Add(
                    new ProcessExecutionOutcome
                    {
                        StepName =
                            candidate.StepName,

                        Status =
                            StepExecutionStatus.Canceled,

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
                    _stateUpdater.ApplyException(
                        context,
                        candidate);

                await _stateRepository.SaveAsync(
                    context,
                    CancellationToken.None);

                outcomes.Add(
                    new ProcessExecutionOutcome
                    {
                        StepName =
                            candidate.StepName,

                        Status =
                            StepExecutionStatus.Exception,

                        Decision =
                            ExecutionDecisionType.ProcessViolation,

                        RuntimeMessages =
                        [
                            StepProcessingMessage.Error(
                            StepProcessingMessageCode.FrameworkException,
                            $"An unexpected exception occurred while executing '{candidate.StepName}'. {exception.Message}")
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
        ExecutionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(decision);

        return new ProcessExecutionOutcome
        {
            StepName =
                candidate.StepName,

            Status =
                MapStatus(
                    decision),

            Decision =
                decision.Type,

            RuntimeMessages =
                decision.Messages,

            BusinessMessages = result.Messages,

            Response = result.Response
        };
    }

    private static StepExecutionStatus MapStatus(
        ExecutionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return decision.Type switch
        {
            ExecutionDecisionType.Continue =>
                StepExecutionStatus.Completed,

            ExecutionDecisionType.Complete =>
                StepExecutionStatus.Completed,

            ExecutionDecisionType.BusinessFailure =>
                StepExecutionStatus.Completed,

            ExecutionDecisionType.ProcessViolation =>
                StepExecutionStatus.Completed,

            ExecutionDecisionType.AwaitingRequiredStep =>
                StepExecutionStatus.Completed,

            ExecutionDecisionType.AwaitingStepSelection =>
                StepExecutionStatus.Completed,

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported execution decision '{decision.Type}'.")
        };
    }

    private static StepProcessingMessage ToStepProcessingMessage(
        ProcessMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.Type switch
        {
            MessageType.Information =>
                StepProcessingMessage.Information(
                    StepProcessingMessageCode.ProcessMessage,
                    message.Message),

            MessageType.Warning =>
                StepProcessingMessage.Warning(
                    StepProcessingMessageCode.ProcessMessage,
                    message.Message),

            MessageType.Error =>
                StepProcessingMessage.Error(
                    StepProcessingMessageCode.ProcessMessage,
                    message.Message),

            _ =>
                StepProcessingMessage.Information(
                    StepProcessingMessageCode.ProcessMessage,
                    message.Message)
        };
    }
}
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

    public ExecutionProcessor(
        IProcessStepInvoker invoker,
        IStepExecutionEvaluator evaluator,
        IProcessStateUpdater stateUpdater,
        IProcessContextStore stateRepository,
        IProcessStepRegistry stepRegistry)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(stateUpdater);
        ArgumentNullException.ThrowIfNull(stateRepository);
        ArgumentNullException.ThrowIfNull(stepRegistry);

        _invoker = invoker;
        _evaluator = evaluator;
        _stateUpdater = stateUpdater;
        _stateRepository = stateRepository;
        _stepRegistry = stepRegistry;
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

        var currentCandidate =
            candidates.FirstOrDefault();

        while (true)
        {
            if (currentCandidate is null)
            {
                break;
            }

            var candidate =
                currentCandidate;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stepContext =
                    context.FindStep(
                        candidate.StepName)
                    ?? throw new InvalidOperationException(
                        $"Step '{candidate.StepName}' was not found in participant state.");

                var processStepContext =
                    new ProcessStepContext(
                        stepContext,
                        GetAvailableNextSteps(
                            candidate));

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
                        candidates);

                context =
                    _stateUpdater.ApplyExecution(
                        context,
                        candidate,
                        decision);

                await _stateRepository.SaveAsync(
                    context,
                    cancellationToken);

                var outcome =
                    CreateOutcome(
                        candidate,
                        result,
                        decision);

                outcomes.Add(
                    outcome);

                currentCandidate =
                    GetNextCandidate(
                        decision);
            }
            catch (OperationCanceledException)
            {
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

                        Messages =
                        [
                            StepProcessingMessage.Error(
                            StepProcessingMessageCode.ExecutionCancelled,
                            "Step execution was cancelled.")
                        ],
                        Response = new ProcessStepEmptyResponse()
                    });

                break;
            }
            catch (Exception exception)
            {
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

                        Messages =
                        [
                            StepProcessingMessage.Error(
                            StepProcessingMessageCode.FrameworkException,
                            $"An unexpected exception occurred while executing '{candidate.StepName}'. {exception.Message}")
                        ],
                        Response = new ProcessStepEmptyResponse()
                    });

                break;
            }
        }

        return new ProcessExecutionResult
        {
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

    private static ProcessExecutionOutcome CreateOutcome(
        StepCandidate candidate,
        ProcessStepInvokerResult result,
        ExecutionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(decision);

        var messages =
            result
                .Messages
                .Select(
                    ToStepProcessingMessage)
                .ToList();

        if (decision.Messages is not null)
        {
            messages.AddRange(
                decision.Messages);
        }

        return new ProcessExecutionOutcome
        {
            StepName =
                candidate.StepName,

            Status =
                MapStatus(
                    decision),

            Decision =
                decision.Type,

            Messages =
                messages,
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

    private IReadOnlyCollection<string> GetAvailableNextSteps(
        StepCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var stepType =
            candidate.Step?.GetType()
            ?? throw new InvalidOperationException(
                $"Step '{candidate.StepName}' does not contain a step instance.");

        return _stepRegistry
            .GetDependents(
                stepType)
            .Select(
                x => x.Metadata.Name)
            .ToArray();
    }
}
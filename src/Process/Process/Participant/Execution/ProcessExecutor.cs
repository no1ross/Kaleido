using Kaleido.Eventing;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;
using Microsoft.Win32;

public interface IExecutionProcessor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessExecutionResult(
    IReadOnlyList<ProcessExecutionOutcome> Outcomes);

public sealed record ProcessExecutionOutcome
{
    public required string StepName
    {
        get;
        init;
    }

    public required StepExecutionStatus Status
    {
        get;
        init;
    }

    public required ExecutionDecisionType Decision
    {
        get;
        init;
    }

    public string? RequiredStep
    {
        get;
        init;
    }

    public IReadOnlyCollection<string> AvailableSteps
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<StepProcessingMessage> Messages
    {
        get;
        init;
    }
        = [];
}

internal sealed class ExecutionProcessor : IExecutionProcessor
{
    private readonly IProcessStepInvoker _invoker;
    private readonly IStepExecutionEvaluator _evaluator;
    private readonly IProcessStateUpdater _stateUpdater;
    private readonly IProcessContextStore _stateRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IProcessStepRegistry _stepRegistry;

    public ExecutionProcessor(
        IProcessStepInvoker invoker,
        IStepExecutionEvaluator evaluator,
        IProcessStateUpdater stateUpdater,
        IProcessContextStore stateRepository,
        IEventPublisher eventPublisher,
        IProcessStepRegistry stepRegistry)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(stateUpdater);
        ArgumentNullException.ThrowIfNull(stateRepository);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(stepRegistry);

        _invoker = invoker;
        _evaluator = evaluator;
        _stateUpdater = stateUpdater;
        _stateRepository = stateRepository;
        _eventPublisher = eventPublisher;
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
            return new ProcessExecutionResult([]);
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

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var stepContext =
                    context.FindStep(
                        currentCandidate.StepName)
                    ?? throw new InvalidOperationException(
                        $"Step '{currentCandidate.StepName}' was not found in participant state.");

                var processStepContext =
                    new ProcessStepContext(
                        stepContext,
                        GetAvailableNextSteps(currentCandidate));

                var result =
                    await _invoker.ExecuteAsync(
                        currentCandidate.Registration!,
                        currentCandidate.Step!,
                        processStepContext,
                        cancellationToken);

                var decision =
                    _evaluator.Evaluate(
                        currentCandidate,
                        result,
                        candidates);

                context =
                    _stateUpdater.ApplyExecution(
                        context,
                        currentCandidate,
                        decision);

                await _stateRepository.SaveAsync(
                    context,
                    cancellationToken);

                var outcome =
                    CreateOutcome(
                        currentCandidate,
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
                        currentCandidate!);

                await _stateRepository.SaveAsync(
                    context,
                    CancellationToken.None);

                outcomes.Add(
                    new ProcessExecutionOutcome
                    {
                        StepName =
                            currentCandidate!.StepName,

                        Status =
                            StepExecutionStatus.Canceled,

                        Decision =
                            ExecutionDecisionType.ProcessViolation,

                        Messages =
                        [
                            StepProcessingMessage.Error(
                                StepProcessingMessageCode.ExecutionCancelled,
                                "Step execution was cancelled.")
                        ]
                    });

                break;
            }
            catch (Exception)
            {
                context =
                    _stateUpdater.ApplyException(
                        context,
                        currentCandidate!);

                await _stateRepository.SaveAsync(
                    context,
                    CancellationToken.None);

                outcomes.Add(
                    new ProcessExecutionOutcome
                    {
                        StepName =
                            currentCandidate!.StepName,

                        Status =
                            StepExecutionStatus.Exception,

                        Decision =
                            ExecutionDecisionType.ProcessViolation,

                        Messages =
                        [
                            StepProcessingMessage.Error(
                                StepProcessingMessageCode.FrameworkException,
                                $"An unexpected exception occurred while executing '{currentCandidate.StepName}'.")
                        ]
                    });

                break;
            }
        }

        return new ProcessExecutionResult(
            outcomes);
    }

    private static StepCandidate? GetNextCandidate(
        ExecutionDecision decision)
    {
        return decision.Type == ExecutionDecisionType.Continue
            ? decision.NextCandidate
            : null;
    }

    private static ProcessExecutionOutcome CreateOutcome(
        StepCandidate candidate,
        ProcessStepResult result,
        ExecutionDecision decision)
    {
        var messages =
            result.Messages
                .Select(
                    ToStepProcessingMessage)
                .ToList();

        if (decision.Message is not null)
        {
            messages.Add(
                decision.Message);
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

            RequiredStep =
                decision.RequiredStep,

            AvailableSteps =
                decision.AvailableSteps,

            Messages =
                messages
        };
    }

    private static StepExecutionStatus MapStatus(
        ExecutionDecision decision)
    {
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
        return StepProcessingMessage.Information(
            StepProcessingMessageCode.ProcessMessage,
            message.Message);
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
            .GetDependents(stepType)
            .Select(x => x.Metadata.Name)
            .ToList();
    }
}
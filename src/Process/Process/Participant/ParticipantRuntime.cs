using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Process.Observability;
using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;

namespace Kaleido.Process.Participant;

internal sealed class ParticipantRuntime
    : IParticipantRuntime
{
    private readonly IProcessContextStore _contextStore;
    private readonly IProcessStateUpdater _stateUpdater;
    private readonly IExecutionPlanner _planner;
    private readonly IExecutionProcessor _processor;
    private readonly IProcessEventFactory _eventFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly IProcessObservability _observability;

    public ParticipantRuntime(
        IProcessContextStore contextStore,
        IProcessStateUpdater stateUpdater,
        IExecutionPlanner planner,
        IExecutionProcessor processor,
        IProcessEventFactory eventFactory,
        IEventPublisher eventPublisher,
        IProcessObservability observability)
    {
        ArgumentNullException.ThrowIfNull(contextStore);
        ArgumentNullException.ThrowIfNull(stateUpdater);
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(eventFactory);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(observability);

        _contextStore = contextStore;
        _stateUpdater = stateUpdater;
        _planner = planner;
        _processor = processor;
        _eventFactory = eventFactory;
        _eventPublisher = eventPublisher;
        _observability = observability;
    }

    public async Task<ParticipantProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        using var observation =
            _observability.BeginExecution(
                new ProcessExecutionObservationDetails(
                    request.Participant.Steps.Count));

        try
        {
            var context =
                await LoadOrCreateContextAsync(
                    request,
                    observation,
                    cancellationToken);

            var plan =
                _planner.BuildPlan(
                    request.Participant,
                    context);

            var executionCandidates =
                GetExecutionCandidates(plan);

            observation.PlanBuilt(
                plan.Candidates.Count,
                executionCandidates.Count);

            await _eventPublisher.PublishAsync(
                _eventFactory.CreatePlanBuilt(
                    context,
                    request,
                    plan,
                    executionCandidates.Count),
                cancellationToken);

            var executionResult =
                await _processor.ExecuteAsync(
                    executionCandidates,
                    context,
                    cancellationToken);

            var result =
                CreateResult(
                    plan,
                    executionResult);

            await _eventPublisher.PublishAsync(
                _eventFactory.CreateExecutionCompleted(
                    executionResult),
                cancellationToken);

            return result;
        }
        catch (Exception exception)
        {
            observation.ExecutionFailed(exception);
            throw;
        }
    }

    private async Task<ParticipantContext> LoadOrCreateContextAsync(
        ProcessRequest request,
        IProcessExecutionObservation observation,
        CancellationToken cancellationToken)
    {
        if (request.ProcessId is null)
        {
            var initializedContext =
                _stateUpdater.Initialize(
                    Guid.NewGuid())
                    with
                {
                    LatestRequestId =
                            request.RequestId
                };

            observation.ContextInitialized(
                initializedContext.ProcessId);

            await _eventPublisher.PublishAsync(
                _eventFactory.CreateProcessCreated(
                    initializedContext,
                    request),
                cancellationToken);

            return initializedContext;
        }

        var context =
            await _contextStore.LoadAsync(
                request.ProcessId.Value,
                cancellationToken);
        
        if (context is null)
        {
            var initializedContext =
                _stateUpdater.Initialize(
                    request.ProcessId.Value)
                    with
                {
                    LatestRequestId =
                            request.RequestId
                };

            observation.ContextInitialized(
                initializedContext.ProcessId);

            await _eventPublisher.PublishAsync(
                _eventFactory.CreateProcessCreated(
                    initializedContext,
                    request),
                cancellationToken);

            return initializedContext;
        }

        observation.ContextLoaded(
            context.ProcessId);

        return _stateUpdater.Reconcile(
            context)
            with
        {
            LatestRequestId =
                    request.RequestId
        };
    }

    private static void ValidateRequest(
        ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.RequestId);
    }

    private static IReadOnlyCollection<StepCandidate> GetExecutionCandidates(
        ExecutionPlanResult plan)
    {
        return plan.Candidates
            .Where(
                x => x.IncludedInExecutionPlan)
            .ToArray();
    }

    private static ParticipantProcessResult CreateResult(
        ExecutionPlanResult plan,
        ProcessExecutionResult executionResult)
    {
        var outcomes =
            executionResult.Outcomes
                .ToDictionary(
                    x => x.StepName,
                    x => x,
                    StringComparer.OrdinalIgnoreCase);

        var steps =
            plan.Candidates
                .Select(
                    candidate =>
                    {
                        outcomes.TryGetValue(
                            candidate.StepName,
                            out var outcome);

                        return new ParticipantStepResult
                        {
                            StepName =
                                candidate.StepName,

                            CandidateStatus =
                                candidate.Status,

                            IncludedInExecutionPlan =
                                candidate.IncludedInExecutionPlan,

                            Response = outcome?.Response,

                            ExecutionStatus =
                                outcome?.Status,

                            Decision =
                                outcome?.Decision,

                            Outcome =
                                GetStepOutcome(outcome?.Status ?? StepExecutionStatus.Pending),

                            RuntimeMessages =
                                MergeMessages(
                                    candidate,
                                    outcome),

                            BusinessMessages = outcome?.BusinessMessages ?? Array.Empty<ProcessMessage>()
                        };
                    })
                .ToArray();

        return new ParticipantProcessResult
        {
            ProcessId =
                executionResult.ProcessId,

            State =
                executionResult.State,

            RequiredStep =
                executionResult.RequiredStep,

            AvailableSteps =
                executionResult.AvailableSteps,

            Steps =
                steps
        };
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

    private static IReadOnlyCollection<StepProcessingMessage> MergeMessages(
        StepCandidate candidate,
        ProcessExecutionOutcome? outcome)
    {
        var messages =
            new List<StepProcessingMessage>();

        messages.AddRange(
            candidate.Messages);

        if (outcome is not null)
        {
            messages.AddRange(
                outcome.RuntimeMessages);
        }

        return messages;
    }
}
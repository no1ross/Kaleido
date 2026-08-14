using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Planning;

namespace Kaleido.Process.Participant.Runtime;

internal sealed class ParticipantRuntime
    : IParticipantRuntime
{
    private readonly IProcessContextStore _contextStore;
    private readonly IProcessStateUpdater _stateUpdater;
    private readonly IExecutionPlanner _planner;
    private readonly IExecutionProcessor _processor;

    public ParticipantRuntime(
        IProcessContextStore contextStore,
        IProcessStateUpdater stateUpdater,
        IExecutionPlanner planner,
        IExecutionProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(contextStore);
        ArgumentNullException.ThrowIfNull(stateUpdater);
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(processor);

        _contextStore = contextStore;
        _stateUpdater = stateUpdater;
        _planner = planner;
        _processor = processor;
    }

    public async Task<ParticipantProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context =
            await LoadOrCreateContextAsync(
                request,
                cancellationToken);

        var plan =
            _planner.BuildPlan(
                request.Participant,
                context);

        var executionResult =
            await _processor.ExecuteAsync(
                GetExecutionCandidates(plan),
                context,
                cancellationToken);

        return CreateResult(
            plan,
            executionResult);
    }

    private async Task<ParticipantContext> LoadOrCreateContextAsync(
        ProcessRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ParticipantProcessId is null)
        {
            return _stateUpdater.Initialize(
                Guid.NewGuid())
                with
            {
                LastestRequestId =
                        request.RequestId
            };
        }

        var context =
            await _contextStore.LoadAsync(
                request.ParticipantProcessId.Value,
                cancellationToken);

        return _stateUpdater.Reconcile(
            context)
            with
        {
            LastestRequestId =
                    request.RequestId
        };
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
            ParticipantProcessId =
                executionResult.ParticipantProcessId,

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
using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;

namespace Kaleido.Processor;

public interface IProcessorRuntime
{
    Task<ProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default);
}

[ExcludeFromCodeCoverage]
public sealed record ProcessRequest
{
    /// <summary>
    /// Uniquely identifies the durable process instance.
    /// Re-submissions for the same process use the same correlation id.
    /// Null creates a new process instance.
    /// </summary>
    public Guid? ProcessId
    {
        get;
        init;
    }

    /// <summary>
    /// Consumer supplied process request data.
    /// </summary>
    public required ProcessorRequest Processor
    {
        get;
        init;
    }

    /// <summary>
    /// Creates a <see cref="ProcessRequest"/> for a single step.
    /// The step name is the step type's name (<c>typeof(TStep).Name</c>), the same
    /// name the registry publishes.
    /// </summary>
    /// <typeparam name="TStep">The process step type.</typeparam>
    /// <param name="step">The step instance containing the input data.</param>
    /// <param name="processId">
    /// Optional process correlation identifier. Pass the same value on each submission
    /// to resume an existing process instance. Omit (or pass <see langword="null"/>) to start a new one.
    /// </param>
    public static ProcessRequest ForStep<TStep>(TStep step, Guid? processId = null)
        where TStep : class, IProcessStep
    {
        ArgumentNullException.ThrowIfNull(step);

        var name = typeof(TStep).Name;

        return new ProcessRequest
        {
            ProcessId = processId,
            Processor = new ProcessorRequest
            {
                Steps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    [name] = step,
                },
            },
        };
    }
}

[ExcludeFromCodeCoverage]
public sealed record ProcessResult
{
    public required Guid ProcessId { get; init; }

    public required ProcessExecutionState State
    {
        get;
        init;
    }

    public string? RequiredStep
    {
        get;
        init;
    }

    /// <summary>The pending information request of <see cref="RequiredStep"/>, if any.</summary>
    public InformationRequest? RequiredInformationRequest
    {
        get;
        init;
    }

    public string? TargetProcessorName
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

    public IReadOnlyCollection<ProcessStepResult> Steps
    {
        get;
        init;
    }
        = [];
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepResult
{
    public required string StepName
    {
        get;
        init;
    }

    public required StepCandidateStatus CandidateStatus
    {
        get;
        init;
    }

    public required bool IncludedInExecutionPlan
    {
        get;
        init;
    }

    public object? Response
    {
        get;
        init;
    }

    public StepExecutionStatus ExecutionStatus
    {
        get;
        init;
    }

    public StepExecutionOutcome Outcome
    {
        get;
        init;
    }

    public IReadOnlyCollection<StepProcessingMessage> RuntimeMessages
    {
        get;
        init;
    }
        = [];

    public IReadOnlyCollection<ProcessMessage> BusinessMessages
    {
        get;
        init;
    }
= [];
}

internal sealed class ProcessorRuntime(
    IKaleidoContextStore contextStore,
    IProcessorStateUpdater stateUpdater,
    IProcessorPlanner planner,
    IProcessorExecutor processor,
    IProcessorEventFactory eventFactory,
    IKaleidoEventOutbox eventOutbox,
    IProcessorObservability observability,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    KaleidoServiceOptions serviceOptions)
    : IProcessorRuntime
{

    public async Task<ProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var observation =
            observability.BeginExecution(
                new ProcessorExecutionObservationDetails(
                    request.Processor.Steps.Count));

        try
        {
            var context = await LoadOrCreateContextAsync(request, observation, cancellationToken);
            var plan = planner.BuildPlan(request.Processor, context);
            var executionCandidates = GetExecutionCandidates(plan);

            observation.PlanBuilt(plan.Candidates.Count, executionCandidates.Length);
            RecordRejectedInformationResponses(plan, observation);
            await eventOutbox.EnqueueAsync(
                eventFactory.CreatePlanBuilt(correlationAccessor.Current, context, request, plan, executionCandidates.Length),
                CancellationToken.None);

            var executionResult =
                await processor.ExecuteAsync(
                    executionCandidates,
                    context,
                    request.Processor,
                    cancellationToken);

            var result = CreateResult(plan, executionResult);

            await eventOutbox.EnqueueAsync(
                eventFactory.CreateExecutionCompleted(correlationAccessor.Current, context, executionResult),
                CancellationToken.None);
            observation.ExecutionCompleted(executionResult.State);

            return result;
        }
        // OperationCanceledException intentionally propagates unrecorded here.
        // Invariant: ONE cancellation signal per event, recorded at the lowest
        // level with full context - that is ProcessorExecutor (stepObservation.Canceled(),
        // where state is saved). Do NOT add an observation.Canceled() catch block at
        // this level; it would double-record every cancellation. See AGENTS.md.
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            observation.ExecutionFailed(exception);
            throw;
        }
    }

    private bool IsHandOff() =>
        correlationAccessor.Current.CallingProcessorName is { Length: > 0 } calling &&
        !string.Equals(calling, serviceOptions.ServiceName, StringComparison.OrdinalIgnoreCase);

    private async Task<ProcessorContext> LoadOrCreateContextAsync(
        ProcessRequest request,
        IProcessorExecutionObservation observation,
        CancellationToken cancellationToken)
    {
        if (request.ProcessId is null)
        {
            return await CreateContextAsync(Guid.NewGuid(), request, observation, cancellationToken);
        }

        var context =
            await contextStore.LoadAsync(
                request.ProcessId.Value,
                cancellationToken);

        // An unknown process id starts a process only on a handoff: a step of another processor
        // continuing the same process here (it stamps itself as the calling processor). Any other
        // unknown id is a mistake (a wrong, removed or reset process) and must not silently start
        // an empty process in its place.
        if (context is null && !IsHandOff())
        {
            throw new KaleidoValidationException(
                ProcessorErrorCodes.ProcessNotFound,
                $"Process '{request.ProcessId.Value}' was not found. To start a new process, omit the process id; an unknown process id only starts a process when another processor hands it off.");
        }

        if (context is null)
        {
            return await CreateContextAsync(request.ProcessId.Value, request, observation, cancellationToken);
        }

        observation.ContextLoaded(
            context.ProcessId);

        return stateUpdater.Reconcile(
            context)
            with
        {
            LatestRequestId = correlationAccessor.Current.RequestId,
        };
    }

    // A new process (or one handed off from another processor): initialized, observed, and saved
    // right away together with its ProcessCreated event - every created process exists and is
    // announced, whether or not a step of this request runs. Two creations of the same id (two
    // handoffs at once) conflict: the second gets 409.
    private async Task<ProcessorContext> CreateContextAsync(
        Guid processId,
        ProcessRequest request,
        IProcessorExecutionObservation observation,
        CancellationToken cancellationToken)
    {
        var initializedContext =
            stateUpdater.Initialize(processId)
                with
            {
                LatestRequestId = correlationAccessor.Current.RequestId,

                // Owner is a tag only (created by), never enforced (ADR 0010).
                Owner = correlationAccessor.Current.CallerName,
            };

        observation.ContextInitialized(
            initializedContext.ProcessId);

        var processCreated =
            eventOutbox.Prepare(
                eventFactory.CreateProcessCreated(correlationAccessor.Current, initializedContext, request));

        var saved = await contextStore.SaveAsync(
            initializedContext,
            processCreated is null ? [] : [processCreated],
            cancellationToken);

        if (processCreated is not null)
        {
            eventOutbox.Saved();
        }

        return saved;
    }
    private static void RecordRejectedInformationResponses(
        ExecutionPlanResult plan,
        IProcessorExecutionObservation observation)
    {
        foreach (var candidate in plan.Candidates.Where(x => x.Registration?.IsInformationStep == true))
        {
            foreach (var message in candidate.Messages.Where(x =>
                         x.Type == MessageType.Error &&
                         x.Code.StartsWith("pro_information_response_", StringComparison.Ordinal)))
            {
                observation.InformationResponseRejected(
                    candidate.StepName,
                    message.Code);
            }
        }
    }

    private static StepCandidate[] GetExecutionCandidates(
        ExecutionPlanResult plan)
    {
        return plan.Candidates
            .Where(
                x => x.IncludedInExecutionPlan)
            .ToArray();
    }

    private static ProcessResult CreateResult(
        ExecutionPlanResult plan,
        ProcessExecutionResult executionResult)
    {
        var outcomes =
            executionResult.Outcomes
                .ToDictionary(
                    x => x.StepName,
                    x => x,
                    StringComparer.OrdinalIgnoreCase);

        return new ProcessResult
        {
            ProcessId = executionResult.ProcessId,
            State = executionResult.State,
            RequiredStep = executionResult.RequiredStep,
            RequiredInformationRequest = executionResult.RequiredInformationRequest,
            TargetProcessorName = executionResult.TargetProcessorName,
            AvailableSteps = executionResult.AvailableSteps,
            Steps = [.. plan.Candidates.Select(candidate => StepResult(candidate, outcomes))],
        };
    }

    // A planned step and, when it ran, its outcome.
    private static ProcessStepResult StepResult(
        StepCandidate candidate,
        Dictionary<string, ProcessExecutionOutcome> outcomes)
    {
        outcomes.TryGetValue(candidate.StepName, out var outcome);

        return new ProcessStepResult
        {
            StepName = candidate.StepName,
            CandidateStatus = candidate.Status,
            IncludedInExecutionPlan = candidate.IncludedInExecutionPlan,
            Response = outcome?.Response,
            ExecutionStatus = outcome?.Status ?? StepExecutionStatus.Pending,
            Outcome = outcome?.Outcome ?? StepExecutionOutcome.Pending,
            RuntimeMessages = MergeMessages(candidate, outcome),
            BusinessMessages = outcome?.BusinessMessages ?? Array.Empty<ProcessMessage>(),
        };
    }
    private static List<StepProcessingMessage> MergeMessages(
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

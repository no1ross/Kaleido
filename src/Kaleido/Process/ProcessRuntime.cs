using System.Reflection;
using Kaleido.Process.Context;
using Kaleido.Process.Eventing;
using Kaleido.Process.Observability;

namespace Kaleido.Process;

public interface IProcessRuntime
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
    /// The step name is resolved from <see cref="ProcessStepAttribute.Name"/>;
    /// if the attribute is absent, <c>typeof(TStep).Name</c> is used as a fallback.
    /// </summary>
    /// <typeparam name="TStep">The process step type.</typeparam>
    /// <param name="step">The step instance containing the input data.</param>
    /// <param name="processId">
    /// Optional process correlation identifier. Pass the same value on each submission
    /// to resume an existing process instance. Omit (or pass <c>null</c>) to start a new one.
    /// </param>
    public static ProcessRequest ForStep<TStep>(TStep step, Guid? processId = null)
        where TStep : class
    {
        ArgumentNullException.ThrowIfNull(step);

        var name =
            typeof(TStep).GetCustomAttribute<ProcessStepAttribute>()?.Name
            ?? typeof(TStep).Name;

        return new ProcessRequest
        {
            ProcessId = processId,
            Processor = new ProcessorRequest
            {
                Steps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    [name] = step
                }
            }
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

[ExcludeFromCodeCoverage]
public sealed record ProcessorRequest
{
    public IReadOnlyDictionary<string, object?> Steps { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

internal sealed class ProcessRuntime(
    IProcessContextStore contextStore,
    IProcessStateUpdater stateUpdater,
    IExecutionPlanner planner,
    IExecutionProcessor processor,
    IProcessEventFactory eventFactory,
    IEventPublisher eventPublisher,
    IProcessObservability observability,
    IKaleidoCorrelationContextAccessor correlationAccessor)
    : IProcessRuntime
{

    public async Task<ProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var observation =
            observability.BeginExecution(
                new ProcessExecutionObservationDetails(
                    request.Processor.Steps.Count));

        try
        {
            var context =
                await LoadOrCreateContextAsync(
                    request,
                    observation,
                    cancellationToken);

            var plan =
                planner.BuildPlan(
                    request.Processor,
                    context);

            var executionCandidates =
                GetExecutionCandidates(plan);

            observation.PlanBuilt(
                plan.Candidates.Count,
                executionCandidates.Count);

            await eventPublisher.PublishAsync(
                eventFactory.CreatePlanBuilt(
                    correlationAccessor.Current,
                    context,
                    request,
                    plan,
                    executionCandidates.Count),
                cancellationToken);

            var executionResult =
                await processor.ExecuteAsync(
                    executionCandidates,
                    context,
                    request.Processor,
                    cancellationToken);

            var result =
                CreateResult(
                    plan,
                    executionResult);

            await eventPublisher.PublishAsync(
                eventFactory.CreateExecutionCompleted(
                    correlationAccessor.Current,
                    context,
                    executionResult),
                cancellationToken);

            observation.ExecutionCompleted();

            return result;
        }
        // OperationCanceledException intentionally propagates unrecorded here.
        // Invariant: ONE cancellation signal per event, recorded at the lowest
        // level with full context — that is ProcessExecutor (stepObservation.Canceled(),
        // where state is saved). Do NOT add an observation.Canceled() catch block at
        // this level; it would double-record every cancellation. See AGENTS.md.
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            observation.ExecutionFailed(exception);
            throw;
        }
    }

    private async Task<ProcessorContext> LoadOrCreateContextAsync(
        ProcessRequest request,
        IProcessExecutionObservation observation,
        CancellationToken cancellationToken)
    {
        var requestId =
            correlationAccessor.Current.RequestId;

        if (request.ProcessId is null)
        {
            var initializedContext =
                stateUpdater.Initialize(
                    Guid.NewGuid())
                    with
                {
                    LatestRequestId = requestId
                };

            observation.ContextInitialized(
                initializedContext.ProcessId);

            await eventPublisher.PublishAsync(
                eventFactory.CreateProcessCreated(
                    correlationAccessor.Current,
                    initializedContext,
                    request),
                cancellationToken);

            return initializedContext;
        }

        var context =
            await contextStore.LoadAsync(
                request.ProcessId.Value,
                cancellationToken);

        if (context is null)
        {
            var initializedContext =
                stateUpdater.Initialize(
                    request.ProcessId.Value)
                    with
                {
                    LatestRequestId = requestId
                };

            observation.ContextInitialized(
                initializedContext.ProcessId);

            await eventPublisher.PublishAsync(
                eventFactory.CreateProcessCreated(
                    correlationAccessor.Current,
                    initializedContext,
                    request),
                cancellationToken);

            return initializedContext;
        }

        observation.ContextLoaded(
            context.ProcessId);

        return stateUpdater.Reconcile(
            context)
            with
        {
            LatestRequestId = requestId
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

        var steps =
            plan.Candidates
                .Select(
                    candidate =>
                    {
                        outcomes.TryGetValue(
                            candidate.StepName,
                            out var outcome);

                        return new ProcessStepResult
                        {
                            StepName =
                                candidate.StepName,

                            CandidateStatus =
                                candidate.Status,

                            IncludedInExecutionPlan =
                                candidate.IncludedInExecutionPlan,

                            Response = outcome?.Response,

                            ExecutionStatus =
                                outcome?.Status ?? StepExecutionStatus.Pending,

                            Outcome =
                                outcome?.Outcome ?? StepExecutionOutcome.Pending,

                            RuntimeMessages =
                                MergeMessages(
                                    candidate,
                                    outcome),

                            BusinessMessages = outcome?.BusinessMessages ?? Array.Empty<ProcessMessage>()
                        };
                    })
                .ToArray();

        return new ProcessResult
        {
            ProcessId =
                executionResult.ProcessId,

            State =
                executionResult.State,

            RequiredStep =
                executionResult.RequiredStep,

            TargetProcessorName =
                executionResult.TargetProcessorName,

            AvailableSteps =
                executionResult.AvailableSteps,

            Steps =
                steps
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
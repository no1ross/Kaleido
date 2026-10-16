using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Processor.Observability;

namespace Kaleido.Processor;

/// <summary>
/// Runs submitted steps of this processor in-process: loads or creates the process, plans the
/// submitted steps, executes the ones the process rules admit, and saves the process state after
/// each executed step (a step its handler rejects leaves the process unchanged and unsaved). The
/// HTTP transport calls it for the process execute endpoints.
/// </summary>
public interface IProcessorRuntime
{
    /// <summary>
    /// Executes the steps in <paramref name="request"/> against the process it identifies, or
    /// against a new process when <see cref="ProcessRequest.ProcessId"/> is <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// A new process is saved right away, before any step runs. Every submitted step becomes a
    /// planned candidate; invalid, already completed or not-yet-allowed steps are reported in the
    /// result instead of being executed. A step whose handler throws (including a
    /// <see cref="KaleidoValidationException"/>) or is cancelled is recorded in the result and the
    /// process state, and stops the request; the exception is not rethrown.
    /// </remarks>
    /// <param name="request">The process to continue (or create) and the steps to submit.</param>
    /// <param name="cancellationToken">
    /// Cancels the execution. A step running when it is cancelled is recorded as canceled and the
    /// process state is saved.
    /// </param>
    /// <returns>The process state after this request and the planning and execution result of every submitted step.</returns>
    /// <exception cref="KaleidoValidationException">
    /// <see cref="ProcessRequest.ProcessId"/> is set but no such process exists, and the request is
    /// not a handoff from another processor (<see cref="ProcessorErrorCodes.ProcessNotFound"/>).
    /// </exception>
    /// <exception cref="KaleidoConcurrencyException">
    /// The process was changed or created concurrently, so its state could not be saved.
    /// </exception>
    Task<ProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A request to <see cref="IProcessorRuntime.ExecuteAsync"/>: the process to continue (or none, to
/// start one) and the steps submitted for it.
/// </summary>
/// <remarks>
/// Use <see cref="ForStep{TStep}"/> to submit a single typed step.
/// </remarks>
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

/// <summary>
/// The result of <see cref="IProcessorRuntime.ExecuteAsync"/>: the process state after the request
/// and the result of every submitted step.
/// </summary>
/// <remarks>
/// <see cref="State"/>, <see cref="RequiredStep"/>, <see cref="RequiredInformationRequest"/>,
/// <see cref="TargetProcessorName"/> and <see cref="AvailableSteps"/> are taken from the process state
/// after the last step this request ran; when no step changed it, they are the state the request
/// started from.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record ProcessResult
{
    /// <summary>The process instance the request ran against; newly generated when the request created the process without an id.</summary>
    public required Guid ProcessId { get; init; }

    /// <summary>The process state after this request.</summary>
    public required ProcessExecutionState State
    {
        get;
        init;
    }

    /// <summary>
    /// The step the process requires next on this processor, or <see langword="null"/> when it does
    /// not wait for a specific step.
    /// </summary>
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

    /// <summary>
    /// The processor the process was handed off to, or <see langword="null"/> when it stays in this
    /// processor. Set when <see cref="State"/> is <see cref="ProcessExecutionState.HandOff"/>.
    /// </summary>
    public string? TargetProcessorName
    {
        get;
        init;
    }

    /// <summary>
    /// The steps the caller may submit next, as stored in the process state. Set when
    /// <see cref="State"/> is <see cref="ProcessExecutionState.AwaitingStepSelection"/>; empty
    /// otherwise, including for a new process on which no step ran (its initial steps are not stored).
    /// </summary>
    public IReadOnlyCollection<string> AvailableSteps
    {
        get;
        init;
    }
        = [];

    /// <summary>
    /// One result per submitted step, in planning order: the steps that passed validation first, in
    /// dependency order, then the rest.
    /// </summary>
    public IReadOnlyCollection<ProcessStepResult> Steps
    {
        get;
        init;
    }
        = [];
}

/// <summary>
/// The result of one submitted step in a <see cref="ProcessResult"/>: how it was planned and, when
/// it ran in this request, its outcome.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessStepResult
{
    /// <summary>The step name as submitted in <see cref="ProcessorRequest.Steps"/>.</summary>
    public required string StepName
    {
        get;
        init;
    }

    /// <summary>
    /// The step's planning result: <see cref="StepCandidateStatus.Built"/> when it was valid,
    /// <see cref="StepCandidateStatus.Invalid"/> when it is not registered, could not be read, failed
    /// validation, misses a dependency, or is not allowed to run next, and
    /// <see cref="StepCandidateStatus.Satisfied"/> when it had already completed and is not repeatable.
    /// </summary>
    public required StepCandidateStatus CandidateStatus
    {
        get;
        init;
    }

    /// <summary>
    /// <see langword="true"/> when the step was put in the execution plan of this request. An
    /// included step runs only when execution reaches it: the first admitted step runs, and later
    /// ones only when the process continues into them.
    /// </summary>
    public required bool IncludedInExecutionPlan
    {
        get;
        init;
    }

    /// <summary>The handler's response payload; <see langword="null"/> when the step did not run, failed with an error, or returned no response.</summary>
    public object? Response
    {
        get;
        init;
    }

    /// <summary>
    /// The step's execution status in this request; <see cref="StepExecutionStatus.Pending"/> when
    /// it did not run.
    /// </summary>
    public StepExecutionStatus ExecutionStatus
    {
        get;
        init;
    }

    /// <summary>
    /// The step's outcome in this request, derived from <see cref="ExecutionStatus"/>;
    /// <see cref="StepExecutionOutcome.Pending"/> when it did not run.
    /// </summary>
    public StepExecutionOutcome Outcome
    {
        get;
        init;
    }

    /// <summary>
    /// The runtime's messages about the step: its planning and validation messages, followed by the
    /// messages from its execution when it ran (a process violation, a validation failure, an
    /// unexpected error or a cancellation).
    /// </summary>
    public IReadOnlyCollection<StepProcessingMessage> RuntimeMessages
    {
        get;
        init;
    }
        = [];

    /// <summary>
    /// The business messages the step's handler returned; empty when the step did not run or
    /// failed with an error.
    /// </summary>
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
        catch (Exception exception) when (exception is not OperationCanceledException && CallerErrorCode(exception) is { } code)
        {
            observation.ExecutionRejected(exception, code);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            observation.ExecutionFailed(exception);
            throw;
        }
    }

    // A request the caller got wrong or must retry is answered to the caller (4xx), not a failure of
    // this service: no Error log, no failure metric (one signal per failure, logged by the transport).
    private static string? CallerErrorCode(Exception exception) =>
        exception switch
        {
            KaleidoValidationException validation => validation.Code,
            KaleidoConcurrencyException conflict => conflict.Code,
            KaleidoAuthorizationException => ProcessorTelemetry.AuthorizationRejectionCode,
            _ => null,
        };

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

            // A tag only (ADR 0010): saved only when a step of this request runs.
            UpdatedBy = correlationAccessor.Current.Caller,
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

                // Tags only (created by / updated by), never enforced (ADR 0010).
                CreatedBy = correlationAccessor.Current.Caller,
                UpdatedBy = correlationAccessor.Current.Caller,
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
                         x.Code.StartsWith(ProcessorErrorCodes.InformationResponsePrefix, StringComparison.Ordinal)))
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

using Kaleido.Process.Context;
using Kaleido.Process.Eventing;
using Kaleido.Process.Observability;
using Kaleido.Process.Recovery;

namespace Kaleido.Process;

/// <summary>
/// Executes typed Process steps in this host: loads or creates an instance, plans the submitted
/// steps, runs those the process rules admit, and saves state after each executed step. A step
/// rejected by its handler leaves the process unchanged. Console jobs and transport adapters both
/// invoke this service; transport parsing is not part of the runtime.
/// </summary>
/// <remarks>
/// Register Process steps with <c>AddProcess()</c>, then resolve this scoped service from the host.
/// Callers supply instances of declared <see cref="IProcessStep"/> types, usually through
/// <see cref="ProcessRequest.ForStep{TStep}"/>. This runtime does not authenticate callers or enforce
/// a step's declared roles and policies: the host must authorize the caller before submitting a step.
/// </remarks>
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
    /// Cancels loading, planning or execution. A cancellation while a step's handler runs can be
    /// recorded as a canceled step; cancellation before a step is admitted propagates.
    /// </param>
    /// <returns>The process state and the planning/execution outcome of every submitted step. Read
    /// <see cref="ProcessResult.Steps"/> rather than inferring a step's success from the overall state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Cancellation interrupted the request before it could complete.</exception>
    /// <exception cref="KaleidoValidationException">
    /// <see cref="ProcessRequest.ProcessId"/> is set but no such process exists, and the request is
    /// not a handoff from another processor (<see cref="ProcessorErrorCodes.ProcessNotFound"/>).
    /// </exception>
    /// <exception cref="ProcessConcurrencyException">
    /// The process was changed or created concurrently, so its state could not be saved.
    /// </exception>
    Task<ProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken);
}

internal sealed class ProcessorRuntime(
    IProcessContextStore contextStore,
    IProcessorTransition transition,
    IProcessorStateUpdater stateUpdater,
    IProcessorPlanner planner,
    IProcessorExecutor processor,
    IProcessorEventFactory eventFactory,
    IKaleidoEventWriter eventWriter,
    IProcessorObservability observability,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    KaleidoServiceOptions serviceOptions,
    IProcessorRecoveryStore recoveryStore)
    : IProcessorRuntime
{

    public async Task<ProcessResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var observation =
            observability.BeginExecution(
                new ProcessorExecutionObservationDetails(
                    request.Processor.Steps.Count));

        try
        {
            ThrowIfRecovering(request);

            var context = await LoadOrCreateContextAsync(request, observation, cancellationToken);
            var plan = planner.BuildPlan(request.Processor, context);
            var executionCandidates = GetExecutionCandidates(plan);

            observation.PlanBuilt(plan.Candidates.Count, executionCandidates.Length);
            RecordRejectedInformationResponses(plan, observation);
            await eventWriter.WriteAsync(
                eventFactory.CreatePlanBuilt(correlationAccessor.Current, context, request, plan, executionCandidates.Length),
                CancellationToken.None);

            var executionResult =
                await processor.ExecuteAsync(
                    executionCandidates,
                    context,
                    cancellationToken);

            var result = CreateResult(plan, executionResult);

            await eventWriter.WriteAsync(
                eventFactory.CreateExecutionCompleted(correlationAccessor.Current, context, executionResult),
                CancellationToken.None);
            observation.ExecutionCompleted(executionResult.State);

            return result;
        }
        // The caller's cancellation intentionally propagates unrecorded here.
        // Invariant: ONE cancellation signal per event, recorded at the lowest
        // level with full context - that is ProcessorExecutor (stepObservation.Canceled(),
        // where state is saved). Do NOT add an observation.Canceled() catch block at
        // this level; it would double-record every cancellation. See AGENTS.md.
        // An OperationCanceledException the caller's token did not raise is a failure (#517).
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (CallerErrorCode(exception) is { } code)
        {
            observation.ExecutionRejected(exception, code);
            throw;
        }
        catch (Exception exception)
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
            ProcessConcurrencyException conflict => conflict.Code,
            ProcessRecoveringException => ProcessorErrorCodes.ProcessRecovering,
            KaleidoAuthorizationException => ProcessorTelemetry.AuthorizationRejectionCode,
            _ => null,
        };

    // An earlier step's outcome is still being saved: nothing runs until it is, so that step's handler
    // is never run a second time (#537). Checked before the store is touched.
    private void ThrowIfRecovering(ProcessRequest request)
    {
        if (request.ProcessId is { } processId && recoveryStore.IsPending(processId))
        {
            throw new ProcessRecoveringException(processId);
        }
    }

    private bool IsHandOff() =>
        correlationAccessor.Current.CallingProcessorName is { Length: > 0 } calling &&
        !string.Equals(calling, serviceOptions.ServiceName, StringComparison.OrdinalIgnoreCase);

    private async Task<ProcessContext> LoadOrCreateContextAsync(
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
                new ProcessKey(serviceOptions.ServiceName, serviceOptions.ServiceVersion, request.ProcessId.Value),
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

        return HandedBack(stateUpdater.Reconcile(
            context))
            with
        {
            LatestRequestId = correlationAccessor.Current.RequestId,

            // A tag only (ADR 0010): saved only when a step of this request runs.
            UpdatedBy = correlationAccessor.Current.Caller,
        };
    }

    // A process handed off to another service is that service's (#512): this one admits none of its own
    // steps, except when the service holding the process hands it back. The process is then this
    // service's again, from where its step graph stands; it is saved so when a step of this request runs.
    private ProcessContext HandedBack(
        ProcessContext context) =>
        context.State == ProcessExecutionState.HandOff &&
        correlationAccessor.Current.CallingProcessorName is { Length: > 0 } calling &&
        string.Equals(calling, context.TargetProcessorName, StringComparison.OrdinalIgnoreCase)
            ? context with { State = ProcessExecutionState.Active, TargetProcessorName = null, TargetProcessorVersion = null }
            : context;

    // A new process (or one handed off from another processor): initialized, observed, and saved
    // right away together with its ProcessCreated event - every created process exists and is
    // announced, whether or not a step of this request runs. Two creations of the same id (two
    // handoffs at once) conflict: the second gets 409.
    private async Task<ProcessContext> CreateContextAsync(
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
            eventWriter.Prepare(
                eventFactory.CreateProcessCreated(correlationAccessor.Current, initializedContext, request));

        return await transition.SaveAsync(initializedContext, processCreated, cancellationToken);
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

    private ProcessResult CreateResult(
        ExecutionPlanResult plan,
        ProcessExecutionResult executionResult)
    {
        var outcomes =
            executionResult.Outcomes
                .ToDictionary(
                    x => x.StepName,
                    x => x,
                    StringComparer.OrdinalIgnoreCase);

        // On the request that hands off, the next steps are the target's answer (#198), by name.
        // The saved state and this result keep the target's selected service version, not a route URL.
        var handOff = executionResult.HandOff;

        return new ProcessResult
        {
            ProcessId = executionResult.ProcessId,
            ServiceVersion = serviceOptions.ServiceVersion,
            State = executionResult.State,
            RequiredStep = handOff is null ? executionResult.RequiredStep : handOff.RequiredStep,
            RequiredInformationRequest = handOff is null ? executionResult.RequiredInformationRequest : handOff.InformationRequest,
            TargetProcessorName = executionResult.TargetProcessorName,
            TargetProcessorVersion = executionResult.TargetProcessorVersion,
            AvailableSteps = handOff is null ? executionResult.AvailableSteps : handOff.AvailableSteps,
            Notice = handOff?.Notice,
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

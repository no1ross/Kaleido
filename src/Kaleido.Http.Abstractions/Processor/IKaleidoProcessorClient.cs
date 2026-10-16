using System.Text.Json;
namespace Kaleido.Http.Processor;

/// <summary>
/// Client for a remote service's Process capabilities: its processor registry, process state, and
/// step execution. Steps are addressed by their published names and resolved against the remote
/// registry (case-insensitive). Obtain one from <see cref="IKaleidoProcessorClientFactory"/>.
/// </summary>
/// <remarks>
/// Calls carry the ambient correlation headers. Every execute call is sent with its own
/// idempotency key, so a retry of the same call does not run the step twice. The HTTP client
/// implementation throws <c>KaleidoHttpClientException</c> when a remote call fails.
/// </remarks>
public interface IKaleidoProcessorClient
{
    /// <summary>
    /// Gets the remote service's processor registry. The registry is fetched on first use and cached
    /// per client name. With a registry time to live configured for the client, the snapshot is
    /// revalidated with the remote once it expires; without one it is kept until it is invalidated
    /// (<see cref="InvalidateRegistryAsync"/>, or a step call answered 404).
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The processors published by the remote registry, as filtered for the caller: usually one,
    /// more when the remote aggregates other services' registries.
    /// </returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the registry
    /// request fails or returns no payload.
    /// </remarks>
    Task<IReadOnlyList<ProcessorRegistryResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the cached registry snapshot of this client's service so the next operation re-fetches
    /// it from the remote endpoint. With a distributed <see cref="Registry.IRegistrySnapshotStore"/>
    /// the shared snapshot is removed for every instance using that store.
    /// </summary>
    /// <param name="cancellationToken">Cancels the removal from the snapshot store.</param>
    /// <returns>A task that completes once the snapshot has been removed.</returns>
    Task InvalidateRegistryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a remote process (<c>GET /{service}/processes/{processId}</c>).
    /// Reading the state never executes a step.
    /// </summary>
    /// <param name="processId">The id of the process on the remote service.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The process state, or <see langword="null"/> when the remote service answers 404 (no such
    /// process).
    /// </returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> for any other
    /// non-success status or an empty payload. A 404 here does not invalidate the cached registry.
    /// </remarks>
    Task<ProcessStateResponse?> GetProcessStateAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one or more steps in a single call to the remote multi-step execute endpoint
    /// (<c>POST /{service}/processes/execute</c>).
    /// </summary>
    /// <param name="request">The steps to submit, each with its JSON input.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The process id, the next required or available steps, and the results of the steps that ran.</returns>
    /// <remarks>
    /// The process to continue is the ambient process id, if any (sent as
    /// <c>X-Kaleido-Process-Id</c>); without one the remote service starts a new process. The HTTP
    /// client implementation throws <c>KaleidoHttpClientException</c> when the remote call fails;
    /// a 400 or 409 carrying Kaleido errors is surfaced with those errors.
    /// </remarks>
    Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a process step remotely. The step is identified by the type name of
    /// <typeparamref name="TStep"/> (<c>typeof(TStep).Name</c>), which must match a step
    /// name in the remote registry — the same name the remote service derives from its
    /// own step type.
    /// </summary>
    /// <typeparam name="TStep">
    /// The step payload type: the remote step type itself, or a local mirror record with
    /// the <b>same type name</b>. Mirrors should not implement <c>IProcessStep</c>, so the
    /// calling service's own discovery does not register them as local steps.
    /// </typeparam>
    /// <param name="processStep">The step input sent to the remote service.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The remote step execution response.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the step name is not in the remote registry or the remote call fails.
    /// </remarks>
    Task<StepExecutionResponse> ExecuteStepAsync<TStep>(
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class;

    /// <summary>
    /// Executes a process step remotely and deserializes its typed response. The step is
    /// identified by the type name of <typeparamref name="TStep"/>
    /// (<c>typeof(TStep).Name</c>), which must match a step name in the remote registry.
    /// </summary>
    /// <typeparam name="TStep">
    /// The step payload type: the remote step type itself, or a local mirror record with
    /// the <b>same type name</b> (mirrors should not implement <c>IProcessStep</c>).
    /// </typeparam>
    /// <typeparam name="TResponse">The step's typed response.</typeparam>
    /// <param name="processStep">The step input sent to the remote service.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The remote step execution response with its typed result.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the step name is not in the remote registry or the remote call fails.
    /// </remarks>
    Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class;

    /// <summary>
    /// Executes a process step by its published name with a JSON payload — for callers that
    /// discover steps at runtime and have no compile-time step type (gateways, AI-tool adapters).
    /// The step URL is resolved from the remote registry by name, exactly as for the typed
    /// overloads.
    /// </summary>
    /// <param name="stepName">The step's published name (case-insensitive).</param>
    /// <param name="processStep">The step input, as the step's JSON object.</param>
    /// <param name="processId">
    /// The process to continue, sent as <c>X-Kaleido-Process-Id</c>; it takes precedence over the
    /// ambient correlation process id. Null keeps today's behavior (the ambient id, if any, or a
    /// new process for an initial step).
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The step execution response, with the step's result as JSON.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the step name is not in the remote registry or the remote call fails.
    /// </remarks>
    Task<StepExecutionResponse<JsonElement>> ExecuteStepAsync(
        string stepName,
        JsonElement processStep,
        Guid? processId = null,
        CancellationToken cancellationToken = default);
}

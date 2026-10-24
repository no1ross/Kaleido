using System.Text.Json;
namespace Kaleido.Http.Processor;

/// <summary>
/// Client for a remote service's Process capabilities: its processor registry, process state, and
/// step execution. Steps are addressed by their published names: a step call goes to
/// <c>/{routePrefix}/processes/steps/{step}</c> without consulting the registry, which is filtered
/// per caller and serves discovery only. Obtain one from <see cref="IKaleidoProcessorClientFactory"/>.
/// </summary>
/// <remarks>
/// Calls carry the ambient correlation headers. Every execute call is sent with an
/// <c>Idempotency-Key</c>: the resilience pipeline re-sends the same request with the same key, so
/// its retries never run the step twice. A <em>new</em> call is a new request: to make a caller's own
/// retry of a logical operation (after a timeout, a restart) safe, pass the same
/// <c>idempotencyKey</c> to <see cref="ExecuteAsync"/> or the by-name
/// <see cref="ExecuteStepAsync(string, JsonElement, Guid?, string?, CancellationToken)"/> each time
/// (ADR 0019). The HTTP client implementation throws <c>KaleidoHttpClientException</c> when a remote
/// call fails.
/// </remarks>
public interface IKaleidoProcessorClient
{
    /// <summary>
    /// Gets the remote service's processor registry, for discovery (step calls do not use it). The
    /// registry is fetched on first use, as filtered for the identity the client's request carries,
    /// and cached per client name. With a registry time to live configured for the client, the snapshot is
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
        CancellationToken cancellationToken);

    /// <summary>
    /// Drops the cached registry snapshot of this client's service so the next operation re-fetches
    /// it from the remote endpoint. With a distributed <see cref="Registry.IRegistrySnapshotStore"/>
    /// the shared snapshot is removed for every instance using that store.
    /// </summary>
    /// <param name="cancellationToken">Cancels the removal from the snapshot store.</param>
    /// <returns>A task that completes once the snapshot has been removed.</returns>
    Task InvalidateRegistryAsync(
        CancellationToken cancellationToken);

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
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes one or more steps in a single call to the remote multi-step execute endpoint
    /// (<c>POST /{service}/processes/execute</c>).
    /// </summary>
    /// <param name="request">The steps to submit, each with its JSON input.</param>
    /// <param name="idempotencyKey">
    /// The <c>Idempotency-Key</c> of this logical call, or <see langword="null"/> for a new key per
    /// call. Sending the same key with the same request again returns the stored answer instead of
    /// running the steps again (scoped per service and caller, up to 200 characters); the same key
    /// with a different request is refused (<c>pro_idempotency_key_reused</c>).
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The process id, the next required or available steps, and the results of the steps that ran.</returns>
    /// <remarks>
    /// The process to continue is the ambient process id, if any (sent as
    /// <c>X-Kaleido-Process-Id</c>); without one the remote service starts a new process. The HTTP
    /// client implementation throws <c>KaleidoHttpClientException</c> when the remote call fails,
    /// with the service's errors when its answer carried any.
    /// </remarks>
    Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes a process step remotely. The step is identified by the type name of
    /// <typeparamref name="TStep"/> (<c>typeof(TStep).Name</c>), which must match a step
    /// name the remote service publishes — the same name it derives from its
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
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the remote call fails; a step the service does not have, or the caller may not run, arrives as the service's own 404 or 403.
    /// </remarks>
    Task<StepExecutionResponse> ExecuteStepAsync<TStep>(
        TStep processStep,
        CancellationToken cancellationToken)
        where TStep : class;

    /// <summary>
    /// Executes a process step remotely and deserializes its typed response. The step is
    /// identified by the type name of <typeparamref name="TStep"/>
    /// (<c>typeof(TStep).Name</c>), which must match a step name the remote service publishes.
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
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the remote call fails; a step the service does not have, or the caller may not run, arrives as the service's own 404 or 403.
    /// </remarks>
    Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(
        TStep processStep,
        CancellationToken cancellationToken)
        where TStep : class;

    /// <summary>
    /// Executes a process step by its published name with a JSON payload — for callers that
    /// discover steps at runtime and have no compile-time step type (gateways, AI-tool adapters).
    /// The step URL is built from the route prefix and the name, exactly as for the typed
    /// overloads.
    /// </summary>
    /// <param name="stepName">The step's published name (routes match it case-insensitively).</param>
    /// <param name="processStep">The step input, as the step's JSON object.</param>
    /// <param name="processId">
    /// The process to continue, sent as <c>X-Kaleido-Process-Id</c>; it takes precedence over the
    /// ambient correlation process id. Null keeps today's behavior (the ambient id, if any, or a
    /// new process for an initial step).
    /// </param>
    /// <param name="idempotencyKey">
    /// The <c>Idempotency-Key</c> of this logical call, or <see langword="null"/> for a new key per
    /// call. Sending the same key with the same step, input and process again returns the stored
    /// answer instead of running the step again (scoped per service and caller, up to 200
    /// characters); the same key with a different request is refused
    /// (<c>pro_idempotency_key_reused</c>). The MCP adapter derives one per logical tool call.
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The step execution response, with the step's result as JSON.</returns>
    /// <remarks>
    /// The HTTP client implementation throws <c>KaleidoHttpClientException</c> when the remote call fails; a step the service does not have, or the caller may not run, arrives as the service's own 404 or 403.
    /// </remarks>
    Task<StepExecutionResponse<JsonElement>> ExecuteStepAsync(
        string stepName,
        JsonElement processStep,
        Guid? processId,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}

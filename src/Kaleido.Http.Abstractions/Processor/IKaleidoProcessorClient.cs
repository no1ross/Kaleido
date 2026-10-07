namespace Kaleido.Http.Processor;

public interface IKaleidoProcessorClient
{
    Task<IReadOnlyList<ProcessorRegistryResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the local registry cache so the next operation re-fetches
    /// the registry from the remote endpoint.
    /// </summary>
    void InvalidateRegistry();

    Task<ProcessStateResponse?> GetProcessStateAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

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
}

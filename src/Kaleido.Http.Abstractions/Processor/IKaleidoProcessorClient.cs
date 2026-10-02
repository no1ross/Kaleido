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

    Task<ProcessStepResponse> GetStepMetadataAsync(
        string stepName,
        CancellationToken cancellationToken = default);

    Task<ProcessStateResponse?> GetProcessStateAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a named process step remotely. <paramref name="stepName"/> must match a step
    /// in the remote registry; when <typeparamref name="TStep"/> carries [ProcessStep], the
    /// attribute's Name is validated against it before the call.
    /// </summary>
    Task<StepExecutionResponse> ExecuteStepAsync<TStep>(
        string stepName,
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class;

    Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(
        string stepName,
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class;
}

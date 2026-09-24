using Kaleido.Http.Process.Contracts;

namespace Kaleido.Http.Process;

public interface IKaleidoProcessClient
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

    Task<StepExecutionResponse> ExecuteStepAsync<TStep>(
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class;

    Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class;
}

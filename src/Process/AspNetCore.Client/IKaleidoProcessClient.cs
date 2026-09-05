using Kaleido.Process.AspNetCore.Contracts;

namespace Kaleido.Process.AspNetCore.Client;

public interface IKaleidoProcessClient
{
    Task<IReadOnlyList<ProcessorRegistryResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default);

    Task<ProcessStepResponse> GetStepMetadataAsync(
        string stepName,
        CancellationToken cancellationToken = default);

    Task<ProcessStateResponse?> GetProcessStateAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    Task<StepExecutionResponse> ExecuteStepAsync<TStep>(
        TStep step,
        Guid? processId = null,
        CancellationToken cancellationToken = default)
        where TStep : class;

    Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(
        TStep step,
        Guid? processId = null,
        CancellationToken cancellationToken = default)
        where TStep : class;
}

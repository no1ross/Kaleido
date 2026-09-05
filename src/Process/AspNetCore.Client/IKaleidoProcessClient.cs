using Kaleido.Process.AspNetCore.Contracts;

namespace Kaleido.Process.AspNetCore.Client;

public interface IKaleidoProcessClient
{
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

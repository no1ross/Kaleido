using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessExecutionService
{
    Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken);

    Task<ProcessExecutionResponse<TResponse>> ExecuteAsync<TProcessStep, TResponse>(
        ExecuteStepRequest<TProcessStep> request,
        CancellationToken cancellationToken);
}

public class ProcessExecutionService : IProcessExecutionService
{
    private readonly IProcessStepRegistry _registry;
    public ProcessExecutionService(IProcessStepRegistry registry)
    {
        _registry = registry;
    }
    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
    public async Task<ProcessExecutionResponse<TResponse>> ExecuteAsync<TProcessStep, TResponse>(
        ExecuteStepRequest<TProcessStep> request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
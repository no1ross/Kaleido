using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessExecutionService
{
    Task<ProcessExecutionContract> ExecuteAsync(
        ExecuteProcessContract request,
        CancellationToken cancellationToken);

    Task<ProcessExecutionContract<TResponse>> ExecuteAsync<TProcessStep, TResponse>(
        ExecuteStepContract<TProcessStep> request,
        CancellationToken cancellationToken);
}
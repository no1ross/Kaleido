using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Registry;
using System.Text.Json;

namespace Kaleido.Process.AspNetCore.Srevices;

public interface IProcessExecutionService
{
    Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken);

    Task<StepExecutionResponse<TResponse>> ExecuteAsync<TProcessStep, TResponse>(
        ExecuteStepRequest<TProcessStep> request,
        CancellationToken cancellationToken);

    Task<StepExecutionResponse> ExecuteAsync<TProcessStep>(
        ExecuteStepRequest<TProcessStep> request,
        CancellationToken cancellationToken);
}

public class ProcessExecutionService : IProcessExecutionService
{
    private readonly IProcessStepRegistry _registry;
    private readonly IParticipantRuntime _runtime;

    public ProcessExecutionService(
        IProcessStepRegistry registry,
        IParticipantRuntime runtime
        )
    {
        _registry = registry;
        _runtime = runtime;
    }
    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
    public async Task<StepExecutionResponse<TResponse>> ExecuteAsync<TProcessStep, TResponse>(
        ExecuteStepRequest<TProcessStep> request,
        CancellationToken cancellationToken)
    {
        var stepName = _registry.GetRegistration(typeof(TProcessStep)).Metadata.Name;

        var processRequest =
            request.ToProcessRequest(
                stepName: stepName,
                requestId: request.RequestId);

        var processResult =
            await _runtime.ExecuteAsync(
                processRequest,
                cancellationToken);

        var stepResult =
            processResult.Steps.Single(x =>
                x.StepName.Equals(
                    stepName,
                    StringComparison.OrdinalIgnoreCase));

        return StepExecutionResponse<TResponse>.Create(
            processResult,
            stepResult,
            _registry);
    }

    public async Task<StepExecutionResponse> ExecuteAsync<TProcessStep>(ExecuteStepRequest<TProcessStep> request, CancellationToken cancellationToken)
    {
        var stepName = _registry.GetRegistration(typeof(TProcessStep)).Metadata.Name;

        var processRequest =
            request.ToProcessRequest(
                stepName: stepName,
                requestId: request.RequestId);

        var processResult =
            await _runtime.ExecuteAsync(
                processRequest,
                cancellationToken);

        var stepResult =
            processResult.Steps.Single(x =>
                x.StepName.Equals(
                    stepName,
                    StringComparison.OrdinalIgnoreCase));

        return StepExecutionResponse.Create(
            processResult,
            stepResult,
            _registry);
    }
}
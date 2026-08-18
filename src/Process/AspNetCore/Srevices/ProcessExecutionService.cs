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
        ArgumentNullException.ThrowIfNull(request);

        var processRequest =
            new ProcessRequest
            {
                ParticipantProcessId = request.ParticipantProcessId,
                RequestId = request.RequestId,
                Participant =
                    new ParticipantRequest
                    {
                        Steps = request.Steps.ToDictionary(
                            x => x.StepName,
                            x => (object?)x.Request,
                            StringComparer.OrdinalIgnoreCase)
                    }
            };

        var processResult =
            await _runtime.ExecuteAsync(
                processRequest,
                cancellationToken);

        return ProcessExecutionResponse.Create(
            processResult,
            _registry);
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
            processResult.Steps
                .Where(x =>
                    x.StepName.Equals(
                        stepName,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.ExecutionStatus is not null)
                .ThenByDescending(x => x.RuntimeMessages.Count)
                .ThenByDescending(x => x.BusinessMessages.Count)
                .First();

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
            processResult.Steps
                .Where(x =>
                    x.StepName.Equals(
                        stepName,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.ExecutionStatus is not null)
                .ThenByDescending(x => x.RuntimeMessages.Count)
                .ThenByDescending(x => x.BusinessMessages.Count)
                .First();

        return StepExecutionResponse.Create(
            processResult,
            stepResult,
            _registry);
    }
}
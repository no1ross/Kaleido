using Kaleido.AspNetCore.Observability;
using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.Registry;
using Microsoft.AspNetCore.Http;
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
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProcessStepRegistry _registry;
    private readonly IProcessorRuntime _runtime;
    private readonly ProcessRouteOptions _routeOptions;


    public ProcessExecutionService(
        IHttpContextAccessor httpContextAccessor,
        IProcessStepRegistry registry,
        IProcessorRuntime runtime,
        ProcessRouteOptions routeOptions
        )
    {
        _httpContextAccessor = httpContextAccessor;
        _registry = registry;
        _runtime = runtime;
        _routeOptions = routeOptions;
    }
    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var processRequest =
            new ProcessRequest
            {
                ProcessId = request.ProcessId,
                RequestId = request.RequestId,
                Processor =
                    new ProcessorRequest
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

        WriteProcessIdHeader(
            processResult.ProcessId);

        return ProcessExecutionResponse.Create(
            processResult,
            _registry,
            _routeOptions);
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

        WriteProcessIdHeader(
            processResult.ProcessId);

        return StepExecutionResponse<TResponse>.Create(
            processResult,
            stepResult,
            _registry,
            _routeOptions);
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

        WriteProcessIdHeader(
            processResult.ProcessId);

        return StepExecutionResponse.Create(
            processResult,
            stepResult,
            _registry,
            _routeOptions);
    }

    private void WriteProcessIdHeader(
        Guid processId)
    {
        _httpContextAccessor.HttpContext?
            .Response.Headers[KaleidoAspNetCoreHeaders.ProcessId] =
                processId.ToString();
    }
}
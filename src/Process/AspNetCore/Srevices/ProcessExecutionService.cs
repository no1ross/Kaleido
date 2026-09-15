using Kaleido.Observability;
using Kaleido.Process.AspNetCore.Contracts;
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
    private readonly KaleidoServiceOptions _serviceOptions;

    public ProcessExecutionService(
        IHttpContextAccessor httpContextAccessor,
        IProcessStepRegistry registry,
        IProcessorRuntime runtime,
        KaleidoServiceOptions serviceOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _registry = registry;
        _runtime = runtime;
        _serviceOptions = serviceOptions;
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

        WriteResponseHeaders(
            processResult.ProcessId);

        return ProcessExecutionResponseFactory.Create(
            processResult,
            _registry,
            _serviceOptions.ServiceName);
    }
    public async Task<StepExecutionResponse<TResponse>> ExecuteAsync<TProcessStep, TResponse>(
        ExecuteStepRequest<TProcessStep> request,
        CancellationToken cancellationToken)
    {
        var stepName = _registry.GetRegistration(typeof(TProcessStep)).Metadata.Name;

        var processRequest =
            request.ToProcessRequest(
                stepName: stepName);

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

        WriteResponseHeaders(
            processResult.ProcessId);

        return StepExecutionResponseFactory.Create<TResponse>(
            processResult,
            stepResult,
            _registry,
            _serviceOptions.ServiceName);
    }

    public async Task<StepExecutionResponse> ExecuteAsync<TProcessStep>(ExecuteStepRequest<TProcessStep> request, CancellationToken cancellationToken)
    {
        var stepName = _registry.GetRegistration(typeof(TProcessStep)).Metadata.Name;

        var processRequest =
            request.ToProcessRequest(
                stepName: stepName);

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

        WriteResponseHeaders(
            processResult.ProcessId);

        return StepExecutionResponseFactory.Create(
            processResult,
            stepResult,
            _registry,
            _serviceOptions.ServiceName);
    }

    private void WriteResponseHeaders(
        Guid processId)
    {
        var headers =
            _httpContextAccessor.HttpContext?.Response.Headers;

        if (headers is null)
        {
            return;
        }

        headers[KaleidoCorrelationHeaders.ProcessId] =
            processId.ToString();

        headers[KaleidoCorrelationHeaders.ProcessorInstanceId] =
            _serviceOptions.InstanceId.ToString();

        headers[KaleidoCorrelationHeaders.SourceProcessor] =
            _serviceOptions.ServiceName;
    }
}
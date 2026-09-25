using Kaleido.Process;
using Kaleido.Process.Registry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Process;

internal interface IProcessExecutionService
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

internal sealed class ProcessExecutionService(
    IHttpContextAccessor httpContextAccessor,
    IProcessStepRegistry registry,
    IProcessRuntime runtime,
    KaleidoServiceOptions serviceOptions,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IProcessExecutionResponseFactory responseFactory,
    ILogger<ProcessExecutionService> logger)
    : IProcessExecutionService
{

    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogDebug(
            "Executing process for processor {ProcessorName} with {StepCount} submitted step(s).",
            serviceOptions.ServiceName,
            request.Steps.Count);

        var processRequest =
            new ProcessRequest
            {
                ProcessId = correlationAccessor.Current.ProcessId,
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
            await runtime.ExecuteAsync(
                processRequest,
                cancellationToken);

        WriteResponseHeaders(
            processResult.ProcessId);

        return responseFactory.CreateExecutionResponse(
            processResult,
            registry,
            serviceOptions.ServiceName);
    }

    public async Task<StepExecutionResponse<TResponse>> ExecuteAsync<TProcessStep, TResponse>(
        ExecuteStepRequest<TProcessStep> request,
        CancellationToken cancellationToken)
    {
        var (processResult, stepResult) =
            await ExecuteStepCoreAsync(request, cancellationToken);

        return responseFactory.CreateStepResponse<TResponse>(
            processResult,
            stepResult,
            registry,
            serviceOptions.ServiceName);
    }

    public async Task<StepExecutionResponse> ExecuteAsync<TProcessStep>(ExecuteStepRequest<TProcessStep> request, CancellationToken cancellationToken)
    {
        var (processResult, stepResult) =
            await ExecuteStepCoreAsync(request, cancellationToken);

        return responseFactory.CreateStepResponse(
            processResult,
            stepResult,
            registry,
            serviceOptions.ServiceName);
    }

    private async Task<(ProcessResult ProcessResult, ProcessStepResult StepResult)>
        ExecuteStepCoreAsync<TProcessStep>(
            ExecuteStepRequest<TProcessStep> request,
            CancellationToken cancellationToken)
    {
        var stepName = registry.GetRegistration(typeof(TProcessStep)).Metadata.Name;

        logger.LogDebug(
            "Executing step {StepName} for processor {ProcessorName}.",
            stepName,
            serviceOptions.ServiceName);

        var processRequest =
            request.ToProcessRequest(
                stepName: stepName,
                processId: correlationAccessor.Current.ProcessId);

        var processResult =
            await runtime.ExecuteAsync(
                processRequest,
                cancellationToken);

        var stepResult =
            processResult.Steps
                .Where(x =>
                    x.StepName.Equals(
                        stepName,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.ExecutionStatus != StepExecutionStatus.Pending)
                .ThenByDescending(x => x.RuntimeMessages.Count)
                .ThenByDescending(x => x.BusinessMessages.Count)
                .First();

        WriteResponseHeaders(
            processResult.ProcessId);

        return (processResult, stepResult);
    }

    private void WriteResponseHeaders(
        Guid processId)
    {
        var headers =
            httpContextAccessor.HttpContext?.Response.Headers;

        if (headers is null)
        {
            return;
        }

        headers[KaleidoCorrelationHeaders.ProcessId] =
            processId.ToString();

        headers[KaleidoCorrelationHeaders.ProcessorInstanceId] =
            serviceOptions.InstanceId.ToString();

        headers[KaleidoCorrelationHeaders.SourceProcessor] =
            serviceOptions.ServiceName;
    }
}

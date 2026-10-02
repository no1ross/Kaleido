using Kaleido.Http.Authorization;
using Kaleido.Processor.Context;
using Kaleido.Processor;
using Kaleido.Processor.Registry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor;

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
    IProcessorStepRegistry registry,
    IProcessorRuntime runtime,
    IProcessorContextStore contextStore,
    KaleidoServiceOptions serviceOptions,
    KaleidoHttpOptions httpOptions,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IProcessExecutionResponseFactory responseFactory,
    IKaleidoAuthorizer authorizer,
    ILogger<ProcessExecutionService> logger)
    : IProcessExecutionService
{

    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Per-step authorization — a multi-step request may submit steps with
        // different [KaleidoAuthorization] declarations; deny the whole request
        // if any submitted step is not authorized for the caller. Unknown step
        // names are left to the runtime's validation, which reports them as
        // request errors rather than authorization failures.
        foreach (var step in request.Steps)
        {
            var registration = registry.Find(step.StepName);

            if (registration is not null)
            {
                await authorizer.AuthorizeAsync(
                    registration.Metadata.Authorization,
                    step.StepName,
                    cancellationToken);
            }
        }

        await AuthorizeProcessAccessAsync(
            correlationAccessor.Current,
            cancellationToken);

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

        await AuthorizeProcessAccessAsync(
            correlationAccessor.Current,
            cancellationToken);

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

    /// <summary>
    /// Process ownership: resuming an owned process requires the owner or a
    /// caller sharing an <c>OwnerRoles</c> entry. With
    /// <see cref="KaleidoHttpOptions.RequireProcessOwnership"/>, creation
    /// additionally requires an authenticated caller so every process is
    /// owned.
    /// </summary>
    private async Task AuthorizeProcessAccessAsync(
        KaleidoCorrelationContext caller,
        CancellationToken cancellationToken)
    {
        if (caller.ProcessId is not null)
        {
            var existing =
                await contextStore.LoadAsync(
                    caller.ProcessId.Value,
                    cancellationToken);

            // Unknown process ids fall through to runtime validation, which
            // creates a new instance under that id.
            if (existing is not null)
            {
                authorizer.AuthorizeProcess(existing);
            }

            return;
        }

        if (httpOptions.RequireProcessOwnership
            && caller.CallerName is null)
        {
            throw new KaleidoAuthorizationException(
                "process creation",
                callerIsAuthenticated: false);
        }
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

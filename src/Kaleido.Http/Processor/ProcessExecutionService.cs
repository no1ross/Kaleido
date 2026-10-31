using System.Text.Json;
using Kaleido.Http.Authorization;
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

internal sealed partial class ProcessExecutionService(
    IHttpContextAccessor httpContextAccessor,
    IProcessorStepRegistry registry,
    IProcessorRuntime runtime,
    KaleidoServiceOptions serviceOptions,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IProcessExecutionResponseFactory responseFactory,
    IKaleidoAuthorizer authorizer,
    IProcessContinuationFilter continuations,
    ILogger<ProcessExecutionService> logger)
    : IProcessExecutionService
{

    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RejectDuplicateSteps(request.Steps);

        // All-or-nothing per-step authorization, before anything runs. The
        // execute route carries no auth of its own, so this is the only gate:
        // every submitted step is checked against its own [KaleidoAuthorization]
        // (undeclared = authenticated caller when enforcing; AllowAnonymous =
        // open), and the first denial rejects the whole request. Unknown step
        // names and empty requests are checked as undeclared (fail closed),
        // then left to the runtime's validation for request errors.
        if (request.Steps.Count == 0)
        {
            await authorizer.AuthorizeAsync(
            authorization: null,
                "process execution",
                cancellationToken);
        }

        foreach (var step in request.Steps)
        {
            await authorizer.AuthorizeAsync(
                registry.Find(step.StepName)?.Metadata.Authorization,
                step.StepName,
                cancellationToken);
        }

        // Every step is built before anything runs: a step that cannot be built rejects the whole
        // request, so nothing runs and no process is created (#344).
        var steps = BuildSteps(request.Steps);

        // No case-level guardrail (ADR 0010): any caller admitted by the
        // steps' declared roles/policies may create or resume the process.
        LogExecutingProcess(logger, serviceOptions.ServiceName, request.Steps.Count);

        var processRequest =
            new ProcessRequest
            {
                ProcessId = correlationAccessor.Current.ProcessId,
                Processor = new ProcessorRequest { Steps = steps },
            };

        var processResult =
            await runtime.ExecuteAsync(
                processRequest,
                cancellationToken);

        WriteResponseHeaders(
            processResult.ProcessId);

        var response = responseFactory.CreateExecutionResponse(
            processResult,
            registry,
            serviceOptions.ServiceName);

        // The caller is described only the steps they may run (#524).
        var continuation = await continuations.FilterAsync(response.ProcessId, response.RequiredStep, response.AvailableSteps, cancellationToken);

        return response with { RequiredStep = continuation.RequiredStep, AvailableSteps = continuation.AvailableSteps, Notice = continuation.Notice };
    }

    // The runtime takes the steps keyed by name: a step named twice is the caller's mistake.
    private static void RejectDuplicateSteps(IReadOnlyCollection<ProcessStepRequest> steps)
    {
        var duplicate = steps
            .GroupBy(x => x.StepName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Skip(1).Any());

        if (duplicate is not null)
        {
            throw new KaleidoValidationException(
                ProcessorErrorCodes.InvalidRequest,
                $"Step '{duplicate.Last().StepName}' is submitted more than once; submit each step once per request.");
        }
    }

    // The transport turns each step's JSON into its step type, with the same rules as the per-step
    // routes (KaleidoJsonOptions); core receives typed steps only.
    private Dictionary<string, object?> BuildSteps(IReadOnlyCollection<ProcessStepRequest> steps)
    {
        var built = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in steps)
        {
            var registration =
                registry.Find(step.StepName)
                ?? throw new KaleidoValidationException(
                    ProcessorErrorCodes.UnknownStep,
                    $"Process step '{step.StepName}' is not registered.");

            built[step.StepName] = ReadStep(step, registration.StepType);
        }

        return built;
    }

    private static object ReadStep(ProcessStepRequest step, Type stepType)
    {
        try
        {
            return step.Request.Deserialize(stepType, KaleidoJsonOptions.Options)
                ?? throw new BadHttpRequestException($"Step '{step.StepName}' has no input; send the step's fields as an object.");
        }
        catch (JsonException exception)
        {
            throw new BadHttpRequestException($"Step '{step.StepName}': {exception.Describe()}", StatusCodes.Status400BadRequest, exception);
        }
    }

    public async Task<StepExecutionResponse<TResponse>> ExecuteAsync<TProcessStep, TResponse>(
        ExecuteStepRequest<TProcessStep> request,
        CancellationToken cancellationToken)
    {
        var (processResult, stepResult) =
            await ExecuteStepCoreAsync(request, cancellationToken);

        return await ForCallerAsync(
            responseFactory.CreateStepResponse<TResponse>(
                processResult,
                stepResult,
                registry,
                serviceOptions.ServiceName),
            cancellationToken);
    }

    public async Task<StepExecutionResponse> ExecuteAsync<TProcessStep>(ExecuteStepRequest<TProcessStep> request, CancellationToken cancellationToken)
    {
        var (processResult, stepResult) =
            await ExecuteStepCoreAsync(request, cancellationToken);

        return await ForCallerAsync(
            responseFactory.CreateStepResponse(
                processResult,
                stepResult,
                registry,
                serviceOptions.ServiceName),
            cancellationToken);
    }

    // The caller is described only the steps they may run (#524).
    private async Task<T> ForCallerAsync<T>(T response, CancellationToken cancellationToken)
        where T : StepExecutionResponse
    {
        var continuation = await continuations.FilterAsync(response.ProcessId, response.RequiredStep, response.AvailableSteps, cancellationToken);

        return response with { RequiredStep = continuation.RequiredStep, AvailableSteps = continuation.AvailableSteps, Notice = continuation.Notice };
    }

    private async Task<(ProcessResult ProcessResult, ProcessStepResult StepResult)>
        ExecuteStepCoreAsync<TProcessStep>(
            ExecuteStepRequest<TProcessStep> request,
            CancellationToken cancellationToken)
    {
        // Authorization is the step endpoint's declared roles/policies only;
        // there is no case-level guardrail on resume (ADR 0010).
        var stepName = registry.GetRegistration(typeof(TProcessStep)).Metadata.Name;

        LogExecutingStep(logger, stepName, serviceOptions.ServiceName);

        // A single-step runtime request carrying the step under its registered name.
        var processRequest = new ProcessRequest
        {
            ProcessId = correlationAccessor.Current.ProcessId,
            Processor = new ProcessorRequest
            {
                Steps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    [stepName] = request.ProcessStep,
                },
            },
        };

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
                .MaxBy(x => (x.ExecutionStatus != StepExecutionStatus.Pending, x.RuntimeMessages.Count, x.BusinessMessages.Count))
            ?? throw new KaleidoFrameworkException(
                ProcessorErrorCodes.MissingStepResult,
                $"Process result for step '{stepName}' contained no entry for that step.");

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
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing process for processor {ProcessorName} with {StepCount} submitted step(s).")]
    private static partial void LogExecutingProcess(ILogger logger, string processorName, int stepCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing step {StepName} for processor {ProcessorName}.")]
    private static partial void LogExecutingStep(ILogger logger, string stepName, string processorName);
}

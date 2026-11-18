using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Http.Server;

internal interface IProcessExecutionService
{
    Task<ProcessResult> ExecuteAsync(ExecuteProcessRequest request, CancellationToken cancellationToken);

    Task<ProcessResult> ExecuteStepAsync<TProcessStep>(ExecuteStepRequest<TProcessStep> request, CancellationToken cancellationToken)
        where TProcessStep : class;
}

internal sealed partial class ProcessExecutionService(
    IHttpContextAccessor httpContextAccessor,
    IProcessorStepRegistry registry,
    IProcessorRuntime runtime,
    KaleidoServiceOptions serviceOptions,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    IProcessContinuationFilter continuations,
    IKaleidoAuthorizer authorizer,
    KaleidoProcessHttpOptions options,
    ILogger<ProcessExecutionService> logger)
    : IProcessExecutionService
{
    public async Task<ProcessResult> ExecuteAsync(ExecuteProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Steps?.Any(step => step is null || string.IsNullOrWhiteSpace(step.StepName)) != false)
        {
            throw new BadHttpRequestException("Every submitted process step must name a registered step.");
        }

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
            await authorizer.AuthorizeAsync(authorization: null, "process execution", cancellationToken);
        }

        foreach (var step in request.Steps)
        {
            await authorizer.AuthorizeAsync(registry.Find(step.StepName)?.Metadata.Authorization, step.StepName, cancellationToken);
        }

        // Every step is built before anything runs: a step that cannot be built rejects the whole
        // request, so nothing runs and no process is created (#344).
        var steps = BuildSteps(request.Steps);

        // No case-level guardrail (ADR 0010): any caller admitted by the
        // steps' declared roles/policies may create or resume the process.
        LogExecutingProcess(logger, serviceOptions.ServiceName, request.Steps.Count);
        var result = await runtime.ExecuteAsync(new ProcessRequest
        {
            ProcessId = correlationAccessor.Current.ProcessId,
            Processor = new ProcessorRequest { Steps = steps },
        }, cancellationToken);
        WriteResponseHeaders(result.ProcessId);
        return await ForCallerAsync(result, cancellationToken);
    }

    public async Task<ProcessResult> ExecuteStepAsync<TProcessStep>(ExecuteStepRequest<TProcessStep> request, CancellationToken cancellationToken)
        where TProcessStep : class
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProcessStep is null)
        {
            throw new BadHttpRequestException("Send the step input as a processStep object.");
        }

        // Authorization is the step endpoint's declared roles/policies only;
        // there is no case-level guardrail on resume (ADR 0010).
        var stepName = registry.GetRegistration(typeof(TProcessStep)).Metadata.Name;
        LogExecutingStep(logger, stepName, serviceOptions.ServiceName);

        // A single-step runtime request carrying the step under its registered name.
        var result = await runtime.ExecuteAsync(new ProcessRequest
        {
            ProcessId = correlationAccessor.Current.ProcessId,
            Processor = new ProcessorRequest
            {
                Steps = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { [stepName] = request.ProcessStep },
            },
        }, cancellationToken);
        if (!result.Steps.Any(step => string.Equals(step.StepName, stepName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new KaleidoFrameworkException(ProcessorErrorCodes.MissingStepResult,
                $"Process result for step '{stepName}' contained no entry for that step.");
        }

        WriteResponseHeaders(result.ProcessId);
        return await ForCallerAsync(result, cancellationToken);
    }

    private async Task<ProcessResult> ForCallerAsync(ProcessResult result, CancellationToken cancellationToken)
    {
        var continuation = await continuations.FilterAsync(result.ProcessId, result.RequiredStep,
            result.AvailableSteps, result.TargetProcessorName, cancellationToken);
        return result with
        {
            RequiredStep = continuation.RequiredStep,
            RequiredInformationRequest = continuation.RequiredStep is null ? null : result.RequiredInformationRequest,
            AvailableSteps = continuation.AvailableSteps,
            Notice = continuation.Notice ?? result.Notice,
            Steps = options.IncludeFrameworkMessages
                ? result.Steps
                : [.. result.Steps.Select(step => step with { RuntimeMessages = [] })],
        };
    }

    // The runtime takes the steps keyed by name: a step named twice is the caller's mistake.
    private static void RejectDuplicateSteps(IReadOnlyCollection<ProcessStepRequest> steps)
    {
        var duplicate = steps.GroupBy(step => step.StepName, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Skip(1).Any());
        if (duplicate is not null)
        {
            throw new KaleidoValidationException(ProcessorErrorCodes.InvalidRequest,
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
            var registration = registry.Find(step.StepName)
                ?? throw new KaleidoValidationException(ProcessorErrorCodes.UnknownStep,
                    $"Process step '{step.StepName}' is not registered.");
            built[step.StepName] = ReadStep(step, registration.StepType);
        }

        return built;
    }

    private static object ReadStep(ProcessStepRequest step, Type stepType)
    {
        try
        {
            var value = step.Request.ValueKind == JsonValueKind.Undefined
                ? null
                : step.Request.Deserialize(stepType, KaleidoJsonOptions.Options);
            return value ?? throw new BadHttpRequestException($"Step '{step.StepName}' has no input; send the step's fields as an object.");
        }
        catch (JsonException exception)
        {
            throw new BadHttpRequestException($"Step '{step.StepName}': {exception.Describe()}", StatusCodes.Status400BadRequest, exception);
        }
    }

    private void WriteResponseHeaders(Guid processId) =>
        httpContextAccessor.HttpContext?.Response.Headers[KaleidoCorrelationHeaders.ProcessId] = processId.ToString();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing process for processor {ProcessorName} with {StepCount} submitted step(s).")]
    private static partial void LogExecutingProcess(ILogger logger, string processorName, int stepCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing step {StepName} for processor {ProcessorName}.")]
    private static partial void LogExecutingStep(ILogger logger, string stepName, string processorName);
}

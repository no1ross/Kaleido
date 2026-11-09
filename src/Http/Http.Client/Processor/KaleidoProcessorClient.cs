using System.Text.Json;

namespace Kaleido.Http.Client.Processor;

// A service's process calls resolve their published routes from the unfiltered central registry.
// The factory pins a version when continuing a process; otherwise the registry's current version is used.
internal sealed class KaleidoProcessorClient(
    string serviceName,
    string? serviceVersion,
    IKaleidoRegistryTargetResolver targets,
    IKaleidoClientTransport transport)
    : IKaleidoProcessorClient
{
    // 404 is an unknown process here, which says nothing about the registry.
    public async Task<ProcessStateResponse?> GetProcessStateAsync(Guid processId, CancellationToken cancellationToken)
    {
        var (processor, route) = await ProcessorAsync(cancellationToken);
        return await transport.SendOrNotFoundAsync<ProcessStateResponse>(
            new KaleidoClientRequest(serviceName, HttpMethod.Get,
                KaleidoPublishedHttpRoute.ProcessState(processor.ProcessStateUrlTemplate, processId, route.GatewayRoute),
                $"Process state request for '{processId}'"), cancellationToken);
    }

    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var (processor, route) = await ProcessorAsync(cancellationToken);
        var call = Execute(KaleidoPublishedHttpRoute.RequirePath(processor.ExecuteUrl, route.GatewayRoute), "Process execute request", request);
        return await transport.SendAsync<ProcessExecutionResponse>(call with { IdempotencyKey = idempotencyKey }, cancellationToken);
    }

    public async Task<StepExecutionResponse> ExecuteStepAsync<TStep>(TStep processStep, CancellationToken cancellationToken)
        where TStep : class =>
        await transport.SendAsync<StepExecutionResponse>(
            Execute(await StepUrlAsync(typeof(TStep).Name, cancellationToken), "Process step request", new ExecuteStepRequest<TStep> { ProcessStep = processStep }),
            cancellationToken);

    public async Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(TStep processStep, CancellationToken cancellationToken)
        where TStep : class =>
        await transport.SendAsync<StepExecutionResponse<TResponse>>(
            Execute(await StepUrlAsync(typeof(TStep).Name, cancellationToken), "Process step request", new ExecuteStepRequest<TStep> { ProcessStep = processStep }),
            cancellationToken);

    public async Task<StepExecutionResponse<JsonElement>> ExecuteStepAsync(
        string stepName,
        JsonElement processStep,
        Guid? processId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var url = await StepUrlAsync(stepName, cancellationToken);
        var request = Execute(url, $"Process step request for '{stepName}'", new ExecuteStepRequest<JsonElement> { ProcessStep = processStep });
        return await transport.SendAsync<StepExecutionResponse<JsonElement>>(
            request with { IdempotencyKey = idempotencyKey, ProcessId = processId }, cancellationToken);
    }

    // Every execute call is idempotent (the caller's key, else its own).
    private KaleidoClientRequest Execute(string url, string description, object body) =>
        new(serviceName, HttpMethod.Post, url, description, body) { Idempotent = true };

    private async Task<string> StepUrlAsync(string stepName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        var (processor, route) = await ProcessorAsync(cancellationToken);
        ProcessStepContract? step = processor.Steps?.FirstOrDefault(x => string.Equals(x.Name, stepName, StringComparison.OrdinalIgnoreCase));
        step ??= processor.InitialSteps.FirstOrDefault(x => string.Equals(x.Name, stepName, StringComparison.OrdinalIgnoreCase));
        return step is not null
            ? KaleidoPublishedHttpRoute.RequirePath(step.ExecuteUrl, route.GatewayRoute)
            : throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The central registry does not publish step '{stepName}' on service '{serviceName}' version '{processor.ServiceVersion}'.");
    }

    private async Task<(ProcessorRegistryResponse Processor, RegistryTransportRoute Route)> ProcessorAsync(CancellationToken cancellationToken)
    {
        var target = await targets.ResolveAsync(serviceName, serviceVersion, ["http"], cancellationToken);
        var processor = target.Registry.Processes.FirstOrDefault(x =>
            string.Equals(x.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ServiceVersion, target.Service.ServiceVersion, StringComparison.Ordinal));
        return processor is not null
            ? (processor, target.Transport)
            : throw new KaleidoRegistryClientException(RegistryClientErrorCodes.RouteUnavailable,
                $"The central registry does not publish a processor for service '{serviceName}' version '{target.Service.ServiceVersion}'.");
    }
}

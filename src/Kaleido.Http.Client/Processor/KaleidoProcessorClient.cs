using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client.Processor;

// One configured client's process calls, as a keyed service (#498): the container passes the client
// name ([ServiceKey]); what to call is this class's, how to send it the transport's. Step URLs come from
// the route prefix, never from the registry: the registry is filtered per caller, so a cached copy
// would answer for whoever fetched it first (#381).
internal sealed class KaleidoProcessorClient(
    [ServiceKey] string clientName,
    IKaleidoClientCatalog catalog,
    IKaleidoClientTransport transport,
    IKaleidoRemoteRegistry remoteRegistry,
    ICorrelationHeaderStamper headerStamper)
    : IKaleidoProcessorClient
{
    private readonly KaleidoClientDefinition _client = catalog.Require(clientName);

    public async Task<IReadOnlyList<ProcessorRegistryResponse>> GetRegistryAsync(CancellationToken cancellationToken) =>
        [.. (await remoteRegistry.GetAsync(_client, headerStamper, cancellationToken)).Processes];

    public Task InvalidateRegistryAsync(CancellationToken cancellationToken) =>
        remoteRegistry.InvalidateAsync(_client.Name, cancellationToken);

    // 404 is an unknown process here, which says nothing about the registry.
    public Task<ProcessStateResponse?> GetProcessStateAsync(Guid processId, CancellationToken cancellationToken) =>
        transport.SendOrNotFoundAsync<ProcessStateResponse>(
            new KaleidoClientRequest(_client, HttpMethod.Get, ProcessContractUrls.ProcessState(_client.RoutePrefix, processId), $"Process state request for '{processId}'"),
            cancellationToken);

    public Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken) =>
        transport.SendAsync<ProcessExecutionResponse>(
            Execute(ProcessContractUrls.Execute(_client.RoutePrefix), "Process execute request", request) with { IdempotencyKey = idempotencyKey },
            cancellationToken);

    public Task<StepExecutionResponse> ExecuteStepAsync<TStep>(TStep processStep, CancellationToken cancellationToken)
        where TStep : class =>
        transport.SendAsync<StepExecutionResponse>(
            Execute(StepUrl(typeof(TStep).Name), "Process step request", new ExecuteStepRequest<TStep> { ProcessStep = processStep }),
            cancellationToken);

    public Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(TStep processStep, CancellationToken cancellationToken)
        where TStep : class =>
        transport.SendAsync<StepExecutionResponse<TResponse>>(
            Execute(StepUrl(typeof(TStep).Name), "Process step request", new ExecuteStepRequest<TStep> { ProcessStep = processStep }),
            cancellationToken);

    public Task<StepExecutionResponse<JsonElement>> ExecuteStepAsync(
        string stepName,
        JsonElement processStep,
        Guid? processId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        var request = Execute(StepUrl(stepName), $"Process step request for '{stepName}'", new ExecuteStepRequest<JsonElement> { ProcessStep = processStep });

        return transport.SendAsync<StepExecutionResponse<JsonElement>>(
            request with { IdempotencyKey = idempotencyKey, ProcessId = processId },
            cancellationToken);
    }

    // Every execute call is idempotent (the caller's key, else its own), and a 404 means the service
    // does not have that step: the discovery snapshot is dropped.
    private KaleidoClientRequest Execute(string url, string description, object body) =>
        new(_client, HttpMethod.Post, url, description, body) { Idempotent = true, InvalidatesRegistryOnNotFound = true };

    // The URL the service publishes for the step: its route segment is the lower-case name, kept one
    // segment whatever the caller passes.
    private string StepUrl(string stepName) =>
        ProcessContractUrls.ExecuteStep(_client.RoutePrefix, Uri.EscapeDataString(stepName.ToLowerInvariant()));
}

using Kaleido.Observability;
using Kaleido.Process.AspNetCore.Contracts;
using System.Net.Http.Json;

namespace Kaleido.Process.AspNetCore.Client;

internal sealed class KaleidoProcessClient : IKaleidoProcessClient
{
    private readonly HttpClient _httpClient;
    private readonly IKaleidoCorrelationContextAccessor _correlation;
    private readonly SemaphoreSlim _registryLock = new(1, 1);
    private IReadOnlyList<ProcessorRegistryResponse>? _registry;

    public KaleidoProcessClient(
        HttpClient httpClient,
        IKaleidoCorrelationContextAccessor correlation)
    {
        _httpClient = httpClient;
        _correlation = correlation;
    }

    public async Task<StepExecutionResponse> ExecuteStepAsync<TStep>(
        TStep step,
        Guid? processId = null,
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync<TStep>(cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
            ProcessId = processId,
            ProcessStep = step
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        StampCorrelationHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StepExecutionResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoProcessClientException(
                       "Process step request succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoProcessClientException(
            $"Process step request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    public async Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(
        TStep step,
        Guid? processId = null,
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync<TStep>(cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
            ProcessId = processId,
            ProcessStep = step
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        StampCorrelationHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StepExecutionResponse<TResponse>>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoProcessClientException(
                       "Process step request succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoProcessClientException(
            $"Process step request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    private async Task<string> ResolveExecuteUrlAsync<TStep>(
        CancellationToken cancellationToken)
    {
        var registry = await EnsureRegistryAsync(cancellationToken);

        // Strip "Step" suffix to get the canonical step name (e.g. CaptureRequestedServiceStep -> CaptureRequestedService)
        var typeName = typeof(TStep).Name;
        var stepName = typeName.EndsWith("Step", StringComparison.OrdinalIgnoreCase)
            ? typeName[..^4]
            : typeName;

        foreach (var processor in registry)
        {
            var match = processor.Steps.FirstOrDefault(
                s => string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                return match.ExecuteUrl;
        }

        throw new InvalidOperationException(
            $"Process step '{stepName}' (from type '{typeName}') was not found in the remote registry.");
    }

    private void StampCorrelationHeaders(HttpRequestMessage request)
    {
        var ctx = _correlation.Current;

        if (!string.IsNullOrWhiteSpace(ctx.RequestId))
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, ctx.RequestId);

        if (ctx.ProcessId.HasValue)
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, ctx.ProcessId.Value.ToString());

        if (!string.IsNullOrWhiteSpace(ctx.SourceProcessorName))
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.SourceProcessor, ctx.SourceProcessorName);

        if (ctx.ProcessorInstanceId.HasValue)
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessorInstanceId, ctx.ProcessorInstanceId.Value.ToString());
    }

    private async Task<IReadOnlyList<ProcessorRegistryResponse>> EnsureRegistryAsync(
        CancellationToken cancellationToken)
    {
        if (_registry is not null)
            return _registry;

        await _registryLock.WaitAsync(cancellationToken);
        try
        {
            if (_registry is not null)
                return _registry;

            var registry = await _httpClient.GetFromJsonAsync<IReadOnlyList<ProcessorRegistryResponse>>(
                "/processes/registry",
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Process registry request succeeded but returned no payload.");

            _registry = registry;
            return _registry;
        }
        finally
        {
            _registryLock.Release();
        }
    }
}

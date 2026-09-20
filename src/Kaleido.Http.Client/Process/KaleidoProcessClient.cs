using Kaleido.Http.Abstractions.Process.Contracts;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Http.Client.Process;

internal sealed class KaleidoProcessClient : IKaleidoProcessClient
{
    private readonly HttpClient _httpClient;
    private readonly IKaleidoCorrelationContextAccessor _correlation;
    private readonly string _serviceName;
    private readonly string _registryUrl;
    private readonly SemaphoreSlim _registryLock = new(1, 1);
    private IReadOnlyList<ProcessorRegistryResponse>? _registry;

    public KaleidoProcessClient(
        HttpClient httpClient,
        IKaleidoCorrelationContextAccessor correlation,
        string serviceName = "")
    {
        _httpClient = httpClient;
        _correlation = correlation;
        _serviceName = serviceName;
        _registryUrl = ProcessContractUrls.Registry(_serviceName);
    }

    public async Task<IReadOnlyList<ProcessorRegistryResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default)
    {
        return await EnsureRegistryAsync(cancellationToken);
    }

    public async Task<ProcessStepResponse> GetStepMetadataAsync(
        string stepName,
        CancellationToken cancellationToken = default)
    {
        var registry = await EnsureRegistryAsync(cancellationToken);

        ProcessStepResponse? match = null;

        foreach (var processor in registry)
        {
            match = processor.Steps.FirstOrDefault(
                s => string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                break;
        }

        if (match is null)
        {
            throw new KaleidoProcessClientException(
                $"Process step '{stepName}' was not found in the remote registry.",
                HttpStatusCode.NotFound);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, match.MetadataUrl);

        StampCorrelationHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProcessStepResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoProcessClientException(
                       $"Process step metadata request for '{stepName}' succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoProcessClientException(
            $"Process step metadata request for '{stepName}' failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    public async Task<ProcessStateResponse?> GetProcessStateAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            ProcessContractUrls.ProcessState(_serviceName, processId));

        StampCorrelationHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProcessStateResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoProcessClientException(
                       $"Process state request for '{processId}' succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoProcessClientException(
            $"Process state request for '{processId}' failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = ProcessContractUrls.Execute(_serviceName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        StampCorrelationHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProcessExecutionResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoProcessClientException(
                       "Process execute request succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoProcessClientException(
            $"Process execute request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    public async Task<StepExecutionResponse> ExecuteStepAsync<TStep>(
        TStep step,
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync<TStep>(cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
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
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync<TStep>(cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
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

        throw new KaleidoProcessClientException(
            $"Process step '{stepName}' (from type '{typeName}') was not found in the remote registry.",
            HttpStatusCode.NotFound);
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

        if (!string.IsNullOrWhiteSpace(ctx.StepName))
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.StepName, ctx.StepName);
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
                _registryUrl,
                cancellationToken)
                ?? throw new KaleidoProcessClientException(
                    "Process registry request succeeded but returned no payload.",
                    HttpStatusCode.InternalServerError);

            _registry = registry;
            return _registry;
        }
        finally
        {
            _registryLock.Release();
        }
    }
}

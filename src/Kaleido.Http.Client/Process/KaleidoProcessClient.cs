using System.Net;
using System.Net.Http.Json;
using Kaleido.Http.Process.Contracts;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Process;

internal sealed class KaleidoProcessClient(
    HttpClient httpClient,
    ICorrelationHeaderStamper headerStamper,
    ILogger<KaleidoProcessClient> logger,
    string serviceName = "")
    : IKaleidoProcessClient, IDisposable
{
    private readonly HttpClientRegistryCache<IReadOnlyList<ProcessorRegistryResponse>> _registryCache = new();

    public void Dispose() => _registryCache.Dispose();

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
            {
                break;
            }
        }

        if (match is null)
        {
            throw new KaleidoHttpClientException(
                HttpClientErrorCodes.NotFound,
                $"Process step '{stepName}' was not found in the remote registry.",
                HttpStatusCode.NotFound);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, match.MetadataUrl);

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProcessStepResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       $"Process step metadata request for '{stepName}' succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            $"Process step metadata request for '{stepName}' failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    public async Task<ProcessStateResponse?> GetProcessStateAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            ProcessContractUrls.ProcessState(serviceName, processId));

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProcessStateResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       $"Process state request for '{processId}' succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            $"Process state request for '{processId}' failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = ProcessContractUrls.Execute(serviceName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProcessExecutionResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       "Process execute request succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            $"Process execute request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    public async Task<StepExecutionResponse> ExecuteStepAsync<TStep>(
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync<TStep>(cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
            ProcessStep = processStep
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StepExecutionResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       "Process step request succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            $"Process step request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    public async Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync<TStep>(cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
            ProcessStep = processStep
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StepExecutionResponse<TResponse>>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       "Process step request succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
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
            {
                return match.ExecuteUrl;
            }
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.NotFound,
            $"Process step '{stepName}' (from type '{typeName}') was not found in the remote registry.",
            HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Sending {Method} {Url} to remote processor '{ServiceName}'.",
            request.Method,
            request.RequestUri,
            serviceName);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Remote processor '{ServiceName}' returned {StatusCode} ({StatusName}) for {Method} {Url}.",
                serviceName,
                (int)response.StatusCode,
                response.StatusCode,
                request.Method,
                request.RequestUri);
        }

        return response;
    }

    private Task<IReadOnlyList<ProcessorRegistryResponse>> EnsureRegistryAsync(
        CancellationToken cancellationToken) =>
        _registryCache.GetOrFetchAsync(FetchRegistryAsync, cancellationToken);

    private async Task<IReadOnlyList<ProcessorRegistryResponse>> FetchRegistryAsync(
        CancellationToken cancellationToken)
    {
        using var registryRequest = new HttpRequestMessage(HttpMethod.Get, ProcessContractUrls.Registry(serviceName));
        headerStamper.Stamp(registryRequest);
        using var registryResponse = await SendAsync(registryRequest, cancellationToken);

        return await registryResponse.Content.ReadFromJsonAsync<IReadOnlyList<ProcessorRegistryResponse>>(
            cancellationToken: cancellationToken)
            ?? throw new KaleidoHttpClientException(
                HttpClientErrorCodes.EmptyResponse,
                "Process registry request succeeded but returned no payload.",
                HttpStatusCode.InternalServerError);
    }
}

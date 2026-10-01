using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Kaleido.Process;
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

    public void InvalidateRegistry() => _registryCache.Reset();

    public async Task<ProcessStepResponse> GetStepMetadataAsync(
        string stepName,
        CancellationToken cancellationToken = default)
    {
        var registry = await EnsureRegistryAsync(cancellationToken);

        ProcessStepResponse? match = null;

        foreach (var processor in registry)
        {
            match = processor.Steps?.FirstOrDefault(
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

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            match.MetadataUrl.RequireValidRegistryUrl(nameof(match.MetadataUrl), httpClient.BaseAddress));

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProcessStepResponse>(
                       KaleidoJsonOptions.Options, cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       $"Process step metadata request for '{stepName}' succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw await CreateFailureAsync(
            response,
            $"Process step metadata request for '{stepName}' failed",
            cancellationToken);
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
                       KaleidoJsonOptions.Options, cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       $"Process state request for '{processId}' succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw await CreateFailureAsync(
            response,
            $"Process state request for '{processId}' failed",
            cancellationToken);
    }

    public async Task<ProcessExecutionResponse> ExecuteAsync(
        ExecuteProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = ProcessContractUrls.Execute(serviceName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request, options: KaleidoJsonOptions.Options)
        };

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProcessExecutionResponse>(
                       KaleidoJsonOptions.Options, cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       "Process execute request succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw await CreateFailureAsync(
            response,
            "Process execute request failed",
            cancellationToken);
    }

    public async Task<StepExecutionResponse> ExecuteStepAsync<TStep>(
        string stepName,
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync<TStep>(stepName, cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
            ProcessStep = processStep
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options)
        };

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StepExecutionResponse>(
                       KaleidoJsonOptions.Options, cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       "Process step request succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw await CreateFailureAsync(
            response,
            "Process step request failed",
            cancellationToken);
    }

    public async Task<StepExecutionResponse<TResponse>> ExecuteStepAsync<TStep, TResponse>(
        string stepName,
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync<TStep>(stepName, cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
            ProcessStep = processStep
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options)
        };

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StepExecutionResponse<TResponse>>(
                       KaleidoJsonOptions.Options, cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       "Process step request succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw await CreateFailureAsync(
            response,
            "Process step request failed",
            cancellationToken);
    }

    private async Task<string> ResolveExecuteUrlAsync<TStep>(
        string stepName,
        CancellationToken cancellationToken)
    {
        var declaredName =
            typeof(TStep).GetCustomAttribute<ProcessStepAttribute>()?.Name;

        if (declaredName is not null &&
            !string.Equals(declaredName, stepName, StringComparison.OrdinalIgnoreCase))
        {
            throw new KaleidoHttpClientException(
                HttpClientErrorCodes.InvalidStepName,
                $"Step name '{stepName}' does not match the [ProcessStep] name '{declaredName}' on type '{typeof(TStep).Name}'.",
                HttpStatusCode.BadRequest);
        }

        var registry = await EnsureRegistryAsync(cancellationToken);

        foreach (var processor in registry)
        {
            var match = processor.Steps?.FirstOrDefault(
                s => string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match.ExecuteUrl.RequireValidRegistryUrl(nameof(match.ExecuteUrl), httpClient.BaseAddress);
            }
        }

        var available = string.Join(
            ", ",
            registry.SelectMany(p => p.Steps ?? []).Select(s => s.Name));

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.NotFound,
            $"Process step '{stepName}' was not found in the remote registry. Available steps: {available}.",
            HttpStatusCode.NotFound);
    }

    private static async Task<KaleidoHttpClientException> CreateFailureAsync(
        HttpResponseMessage response,
        string description,
        CancellationToken cancellationToken)
    {
        KaleidoErrorResponse? errorResponse = null;

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            try
            {
                errorResponse = await response.Content
                    .ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, cancellationToken);
            }
            catch (System.Text.Json.JsonException)
            {
                // Non-Kaleido or non-JSON error body — fall through to RequestFailed.
            }
        }

        if (errorResponse?.Errors.Count > 0)
        {
            return new KaleidoHttpClientException(
                HttpClientErrorCodes.ValidationFailed,
                $"{description}: {string.Join(" ", errorResponse.Errors.Select(e => e.Message))}",
                response.StatusCode,
                errorResponse.Errors);
        }

        return new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            $"{description} with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
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
            KaleidoJsonOptions.Options, cancellationToken)
            ?? throw new KaleidoHttpClientException(
                HttpClientErrorCodes.EmptyResponse,
                "Process registry request succeeded but returned no payload.",
                HttpStatusCode.InternalServerError);
    }
}

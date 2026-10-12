using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Processor;

internal sealed partial class KaleidoProcessorClient(
    HttpClient httpClient,
    ICorrelationHeaderStamper headerStamper,
    ILogger<KaleidoProcessorClient> logger,
    KaleidoRemoteRegistry remoteRegistry,
    string clientName,
    string serviceName = "",
    TimeSpan? registryTtl = null)
    : IKaleidoProcessorClient
{
    public async Task<IReadOnlyList<ProcessorRegistryResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default)
    {
        return await EnsureRegistryAsync(cancellationToken);
    }

    public void InvalidateRegistry() => remoteRegistry.Invalidate(clientName);

    public async Task<ProcessStateResponse?> GetProcessStateAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            ProcessContractUrls.ProcessState(serviceName, processId));

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken, capabilityCall: false);

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
            Content = JsonContent.Create(request, options: KaleidoJsonOptions.Options),
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
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync(typeof(TStep).Name, cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
            ProcessStep = processStep,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options),
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
        TStep processStep,
        CancellationToken cancellationToken = default)
        where TStep : class
    {
        var url = await ResolveExecuteUrlAsync(typeof(TStep).Name, cancellationToken);

        var body = new ExecuteStepRequest<TStep>
        {
            ProcessStep = processStep,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options),
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

    public async Task<StepExecutionResponse<JsonElement>> ExecuteStepAsync(
        string stepName,
        JsonElement processStep,
        Guid? processId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        var url = await ResolveExecuteUrlAsync(stepName, cancellationToken);

        var body = new ExecuteStepRequest<JsonElement>
        {
            ProcessStep = processStep,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: KaleidoJsonOptions.Options),
        };

        headerStamper.Stamp(httpRequest);

        // An explicit process id wins over the ambient correlation one.
        if (processId is { } id)
        {
            httpRequest.Headers.Remove(KaleidoCorrelationHeaders.ProcessId);
            httpRequest.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<StepExecutionResponse<JsonElement>>(
                       KaleidoJsonOptions.Options, cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       $"Process step request for '{stepName}' succeeded but returned no payload.",
                       response.StatusCode);
        }

        throw await CreateFailureAsync(
            response,
            $"Process step request for '{stepName}' failed",
            cancellationToken);
    }

    private async Task<string> ResolveExecuteUrlAsync(
        string stepName,
        CancellationToken cancellationToken)
    {
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
                $"{description}: {string.Join(' ', errorResponse.Errors.Select(e => e.Message))}",
                response.StatusCode,
                errorResponse.Errors);
        }

        return new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            string.Create(CultureInfo.InvariantCulture, $"{description} with status code {(int)response.StatusCode} ({response.StatusCode})."),
            response.StatusCode);
    }

    // A capability call answered 404 means the service no longer has that step (it was deployed
    // without it): the registry snapshot is dropped so the next read loads the current one. The process
    // state call answers 404 for an unknown process, which says nothing about the registry.
    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool capabilityCall = true)
    {
        LogSending(logger, request.Method, request.RequestUri, serviceName);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogRemoteNonSuccess(logger, serviceName, (int)response.StatusCode, response.StatusCode, request.Method, request.RequestUri);
        }

        if (capabilityCall && response.StatusCode == HttpStatusCode.NotFound)
        {
            remoteRegistry.Invalidate(clientName);
        }

        return response;
    }

    private async Task<IReadOnlyList<ProcessorRegistryResponse>> EnsureRegistryAsync(
        CancellationToken cancellationToken) =>
        [.. (await remoteRegistry.GetAsync(clientName, serviceName, registryTtl, headerStamper, cancellationToken)).Processes];

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending {Method} {Url} to remote processor '{ServiceName}'.")]
    private static partial void LogSending(ILogger logger, HttpMethod method, Uri? url, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Remote processor '{ServiceName}' returned {StatusCode} ({StatusName}) for {Method} {Url}.")]
    private static partial void LogRemoteNonSuccess(ILogger logger, string serviceName, int statusCode, HttpStatusCode statusName, HttpMethod method, Uri? url);
}

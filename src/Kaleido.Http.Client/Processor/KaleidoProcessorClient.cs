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

    public Task InvalidateRegistryAsync(
        CancellationToken cancellationToken = default) =>
        remoteRegistry.InvalidateAsync(clientName, cancellationToken);

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
        var url = StepUrl(typeof(TStep).Name);

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
        var url = StepUrl(typeof(TStep).Name);

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

        var url = StepUrl(stepName);

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

    private static async Task<KaleidoHttpClientException> CreateFailureAsync(
        HttpResponseMessage response,
        string description,
        CancellationToken cancellationToken)
    {
        KaleidoErrorResponse? errorResponse = null;

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
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
                response.StatusCode == HttpStatusCode.Conflict ? HttpClientErrorCodes.Conflict : HttpClientErrorCodes.ValidationFailed,
                $"{description}: {string.Join(' ', errorResponse.Errors.Select(e => e.Message))}",
                response.StatusCode,
                errorResponse.Errors);
        }

        return new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            string.Create(CultureInfo.InvariantCulture, $"{description} with status code {(int)response.StatusCode} ({response.StatusCode})."),
            response.StatusCode);
    }

    // Step URLs come from the route prefix, never from the registry: the registry is filtered per
    // caller, so a cached copy would answer for whoever fetched it first (#381). A capability call
    // answered 404 means the service does not have that step (perhaps deployed without it): the
    // discovery snapshot is dropped so the next registry read loads the current one. The process state
    // call answers 404 for an unknown process, which says nothing about the registry.
    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool capabilityCall = true)
    {
        // Every execute call is idempotent: one key per call, so a retry of this request (the
        // resilience pipeline re-sends the same message) gets the stored answer instead of running the
        // step a second time.
        if (request.Method == HttpMethod.Post && !request.Headers.Contains(KaleidoIdempotencyHeaders.IdempotencyKey))
        {
            request.Headers.Add(KaleidoIdempotencyHeaders.IdempotencyKey, Guid.NewGuid().ToString("N"));
        }

        LogSending(logger, request.Method, request.RequestUri, serviceName);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogRemoteNonSuccess(logger, serviceName, (int)response.StatusCode, response.StatusCode, request.Method, request.RequestUri);
        }

        if (capabilityCall && response.StatusCode == HttpStatusCode.NotFound)
        {
            await remoteRegistry.InvalidateAsync(clientName, cancellationToken);
        }

        return response;
    }

    // The URL the service publishes for the step: its route segment is the lower-case name, kept one
    // segment whatever the caller passes.
    private string StepUrl(string stepName) =>
        ProcessContractUrls.ExecuteStep(serviceName, Uri.EscapeDataString(stepName.ToLowerInvariant()));

    private async Task<IReadOnlyList<ProcessorRegistryResponse>> EnsureRegistryAsync(
        CancellationToken cancellationToken) =>
        [.. (await remoteRegistry.GetAsync(clientName, serviceName, registryTtl, headerStamper, cancellationToken)).Processes];

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending {Method} {Url} to remote processor '{ServiceName}'.")]
    private static partial void LogSending(ILogger logger, HttpMethod method, Uri? url, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Remote processor '{ServiceName}' returned {StatusCode} ({StatusName}) for {Method} {Url}.")]
    private static partial void LogRemoteNonSuccess(ILogger logger, string serviceName, int statusCode, HttpStatusCode statusName, HttpMethod method, Uri? url);
}

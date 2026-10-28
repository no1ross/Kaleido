using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

/// <summary>
/// Sends one Kaleido client request to a configured remote service and reads its answer (#498):
/// the named <c>HttpClient</c>, the correlation headers, the idempotency key, logging, failure
/// mapping (<see cref="KaleidoHttpClientException"/>) and deserialization. The processor and queryable
/// clients only say what to call.
/// </summary>
internal interface IKaleidoClientTransport
{
    /// <summary>The deserialized answer of a successful call; a failed call throws its mapped failure.</summary>
    Task<TResponse> SendAsync<TResponse>(KaleidoClientRequest request, CancellationToken cancellationToken)
        where TResponse : class;

    /// <summary>As <see cref="SendAsync{TResponse}"/>, but a 404 answer returns null.</summary>
    Task<TResponse?> SendOrNotFoundAsync<TResponse>(KaleidoClientRequest request, CancellationToken cancellationToken)
        where TResponse : class;
}

internal sealed partial class KaleidoClientTransport(
    IHttpClientFactory httpClientFactory,
    ICorrelationHeaderStamper headerStamper,
    IKaleidoRemoteRegistry remoteRegistry,
    ILogger<KaleidoClientTransport> logger)
    : IKaleidoClientTransport
{
    public async Task<TResponse> SendAsync<TResponse>(KaleidoClientRequest request, CancellationToken cancellationToken)
        where TResponse : class =>
        await SendCoreAsync<TResponse>(request, notFoundIsNull: false, cancellationToken)
        ?? throw new KaleidoHttpClientException(
            HttpClientErrorCodes.EmptyResponse,
            $"{request.Description} succeeded but returned no payload.",
            HttpStatusCode.OK);

    public Task<TResponse?> SendOrNotFoundAsync<TResponse>(KaleidoClientRequest request, CancellationToken cancellationToken)
        where TResponse : class =>
        SendCoreAsync<TResponse>(request, notFoundIsNull: true, cancellationToken);

    private async Task<TResponse?> SendCoreAsync<TResponse>(
        KaleidoClientRequest request,
        bool notFoundIsNull,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var message = Message(request);
        using var response = await SendMessageAsync(request, message, cancellationToken);

        if (notFoundIsNull && response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<TResponse>(KaleidoJsonOptions.Options, cancellationToken)
                ?? throw new KaleidoHttpClientException(
                    HttpClientErrorCodes.EmptyResponse,
                    $"{request.Description} succeeded but returned no payload.",
                    response.StatusCode);
        }

        throw await response.ToClientFailureAsync($"{request.Description} failed", cancellationToken);
    }

    private HttpRequestMessage Message(KaleidoClientRequest request)
    {
        var message = new HttpRequestMessage(request.Method, request.Url);

        if (request.Body is not null)
        {
            message.Content = JsonContent.Create(request.Body, request.Body.GetType(), options: KaleidoJsonOptions.Options);
        }

        headerStamper.Stamp(message);

        // An explicit process id wins over the ambient correlation one.
        if (request.ProcessId is { } processId)
        {
            message.Headers.Remove(KaleidoCorrelationHeaders.ProcessId);
            message.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString());
        }

        // The caller's key for this logical call (#440). Not validated here: the service answers an
        // over-long key with 400 pro_invalid_idempotency_key. Without one, an execute call gets its own
        // key, so a retry of this request (the resilience pipeline re-sends the same message) gets the
        // stored answer instead of running the step a second time.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            message.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, request.IdempotencyKey);
        }
        else if (request.Idempotent)
        {
            message.Headers.Add(KaleidoIdempotencyHeaders.IdempotencyKey, Guid.NewGuid().ToString("N"));
        }

        return message;
    }

    private async Task<HttpResponseMessage> SendMessageAsync(
        KaleidoClientRequest request,
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        LogSending(logger, request.Method, request.Url, request.Client.Name, request.Client.RoutePrefix);

        // IHttpClientFactory owns the client lifetime - never dispose it.
        var response = await httpClientFactory.CreateClient(request.Client.Name).SendAsync(message, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogRemoteNonSuccess(logger, request.Client.RoutePrefix, (int)response.StatusCode, response.StatusCode, request.Method, request.Url);
        }

        // A capability call answered 404: the service does not have that capability (perhaps deployed
        // without it), so the discovery snapshot is dropped and the next registry read loads the current one.
        if (request.InvalidatesRegistryOnNotFound && response.StatusCode == HttpStatusCode.NotFound)
        {
            await remoteRegistry.InvalidateAsync(request.Client.Name, cancellationToken);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending {Method} {Url} through Kaleido client '{ClientName}' to service '{ServiceName}'.")]
    private static partial void LogSending(ILogger logger, HttpMethod method, string url, string clientName, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Remote Kaleido service '{ServiceName}' returned {StatusCode} ({StatusName}) for {Method} {Url}.")]
    private static partial void LogRemoteNonSuccess(ILogger logger, string serviceName, int statusCode, HttpStatusCode statusName, HttpMethod method, string url);
}

/// <summary>One request a Kaleido client sends; see <see cref="IKaleidoClientTransport"/>.</summary>
/// <param name="Client">The configured client it goes through.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Url">The URL, relative to the client's base address.</param>
/// <param name="Description">What the call is, for messages: "Process execute request".</param>
/// <param name="Body">The JSON body, or null.</param>
internal sealed record KaleidoClientRequest(
    KaleidoClientDefinition Client,
    HttpMethod Method,
    string Url,
    string Description,
    object? Body = null)
{
    /// <summary>The caller's idempotency key for this logical call, if any.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Without a caller's key, the request gets its own (execute calls).</summary>
    public bool Idempotent { get; init; }

    /// <summary>The process the call is for, overriding the ambient one.</summary>
    public Guid? ProcessId { get; init; }

    /// <summary>A 404 answer drops the client's cached registry (capability calls).</summary>
    public bool InvalidatesRegistryOnNotFound { get; init; }
}

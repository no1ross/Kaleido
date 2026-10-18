using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Queryable;

internal sealed partial class KaleidoQueryableClient(
    HttpClient httpClient,
    ICorrelationHeaderStamper headerStamper,
    ILogger<KaleidoQueryableClient> logger,
    KaleidoRemoteRegistry remoteRegistry,
    string clientName,
    string callerServiceName = "",
    TimeSpan? registryTtl = null)
    : IKaleidoQueryableClient
{
    public async Task<IReadOnlyList<QueryableSourceResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default)
    {
        return await EnsureRegistryAsync(cancellationToken);
    }

    public Task InvalidateRegistryAsync(
        CancellationToken cancellationToken = default) =>
        remoteRegistry.InvalidateAsync(clientName, cancellationToken);

    public Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string source,
        string view,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken = default)
        where TParameters : class
        where TView : class =>
        SendQueryAsync<TView>(QueryableContractUrls.QueryViewQuery(callerServiceName, Segment(source), Segment(view)), request, cancellationToken, source, view);

    public Task<QueryResult<TResult>> QuerySourceAsync<TResult>(
        string source,
        QueryApiRequest request,
        CancellationToken cancellationToken = default)
        where TResult : class =>
        QuerySourceCoreAsync<TResult>(source, request, cancellationToken);

    public Task<QueryResult<TResult>> QuerySourceAsync<TParameters, TResult>(
        string source,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken = default)
        where TParameters : class
        where TResult : class =>
        QuerySourceCoreAsync<TResult>(source, request, cancellationToken);

    private Task<QueryResult<TResult>> QuerySourceCoreAsync<TResult>(
        string source,
        object request,
        CancellationToken cancellationToken)
        where TResult : class =>
        SendQueryAsync<TResult>(QueryableContractUrls.QuerySourceQuery(callerServiceName, Segment(source)), request, cancellationToken, source, view: null);

    // The route segment the service publishes for a source or view: the lower-case name, kept one
    // segment whatever the caller passes.
    private static string Segment(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Uri.EscapeDataString(name.ToLowerInvariant());
    }

    // Query URLs come from the route prefix, never from the registry: the registry is filtered per
    // caller, so a cached copy would answer for whoever fetched it first (#381). An unknown or
    // forbidden source or view gets the service's own answer (404 / 403).
    private async Task<QueryResult<TView>> SendQueryAsync<TView>(
        string url,
        object request,
        CancellationToken cancellationToken,
        string? source = null,
        string? view = null)
        where TView : class
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            url)
        {
            Content = JsonContent.Create(request, options: KaleidoJsonOptions.Options),
        };

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<QueryResult<TView>>(
                       KaleidoJsonOptions.Options,
                       cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       $"{callerServiceName} tried to call {FormatTarget(source, view)}, but the request succeeded and returned no payload.",
                       response.StatusCode);
        }

        var errorResponse =
            response.StatusCode == HttpStatusCode.BadRequest
                ? await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(
                    KaleidoJsonOptions.Options,
                    cancellationToken)
                : null;

        if (errorResponse?.Errors.Count > 0)
        {
            throw new KaleidoHttpClientException(
                HttpClientErrorCodes.ValidationFailed,
                $"{callerServiceName} tried to call {FormatTarget(source, view)}, but the request failed: {string.Join(' ', errorResponse.Errors.Select(e => e.Message))}",
                response.StatusCode,
                errorResponse.Errors);
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            string.Create(CultureInfo.InvariantCulture, $"{callerServiceName} tried to call {FormatTarget(source, view)}, but the request failed with status code {(int)response.StatusCode} ({response.StatusCode})."),
            response.StatusCode);
    }

    private static string FormatTarget(string? source, string? view)
    {
        if (!string.IsNullOrEmpty(view))
        {
            return $"view '{view}' on source '{source}'";
        }

        return $"source '{source}'";
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LogSending(logger, request.Method, request.RequestUri, callerServiceName);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogRemoteNonSuccess(logger, callerServiceName, (int)response.StatusCode, response.StatusCode, request.Method, request.RequestUri);
        }

        // 404: the service does not have that source or view (perhaps deployed without it); the
        // discovery snapshot is dropped so the next registry read loads the current one.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            await remoteRegistry.InvalidateAsync(clientName, cancellationToken);
        }

        return response;
    }

    private async Task<IReadOnlyList<QueryableSourceResponse>> EnsureRegistryAsync(
        CancellationToken cancellationToken) =>
        [.. (await remoteRegistry.GetAsync(clientName, callerServiceName, registryTtl, headerStamper, cancellationToken)).Queryables];

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending {Method} {Url} to remote queryable '{ServiceName}'.")]
    private static partial void LogSending(ILogger logger, HttpMethod method, Uri? url, string serviceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Remote queryable '{ServiceName}' returned {StatusCode} ({StatusName}) for {Method} {Url}.")]
    private static partial void LogRemoteNonSuccess(ILogger logger, string serviceName, int statusCode, HttpStatusCode statusName, HttpMethod method, Uri? url);
}

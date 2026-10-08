using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Queryable;

internal sealed class KaleidoQueryableClient(
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

    public void InvalidateRegistry() => remoteRegistry.Invalidate(clientName);

    public async Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string source,
        string view,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken = default)
        where TParameters : class
        where TView : class
    {
        var sourceEntry = await FindSourceAsync(source, view, cancellationToken);

        var viewEntry = sourceEntry.Views.FirstOrDefault(
            v => string.Equals(v.Name, view, StringComparison.OrdinalIgnoreCase))
            ?? throw new KaleidoHttpClientException(
                HttpClientErrorCodes.NotFound,
                $"{callerServiceName} tried to call view '{view}' on source '{source}', but the view was not found in the remote registry.",
                HttpStatusCode.NotFound);

        return await SendQueryAsync<TView>(viewEntry.QueryUrl, request, cancellationToken, source, view);
    }

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

    private async Task<QueryResult<TResult>> QuerySourceCoreAsync<TResult>(
        string source,
        object request,
        CancellationToken cancellationToken)
        where TResult : class
    {
        var sourceEntry = await FindSourceAsync(source, null, cancellationToken);

        if (string.IsNullOrEmpty(sourceEntry.QueryUrl))
        {
            throw new KaleidoHttpClientException(
                HttpClientErrorCodes.NotFound,
                $"{callerServiceName} tried to call source '{source}', but the remote registry does not expose a query URL for it to this caller.",
                HttpStatusCode.NotFound);
        }

        return await SendQueryAsync<TResult>(sourceEntry.QueryUrl, request, cancellationToken, source, null);
    }

    private async Task<QueryableSourceResponse> FindSourceAsync(
        string source,
        string? view,
        CancellationToken cancellationToken)
    {
        var registry = await EnsureRegistryAsync(cancellationToken);

        return registry.FirstOrDefault(
            r => string.Equals(r.Name, source, StringComparison.OrdinalIgnoreCase))
            ?? throw new KaleidoHttpClientException(
                HttpClientErrorCodes.NotFound,
                $"{callerServiceName} tried to call {FormatTarget(source, view)}, but the source was not found in the remote registry.",
                HttpStatusCode.NotFound);
    }

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
            url.RequireValidRegistryUrl(nameof(url), httpClient.BaseAddress))
        {
            Content = JsonContent.Create(request, options: KaleidoJsonOptions.Options)
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
                $"{callerServiceName} tried to call {FormatTarget(source, view)}, but the request failed: {string.Join(" ", errorResponse.Errors.Select(e => e.Message))}",
                response.StatusCode,
                errorResponse.Errors);
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            $"{callerServiceName} tried to call {FormatTarget(source, view)}, but the request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
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
        logger.LogDebug(
            "Sending {Method} {Url} to remote queryable '{ServiceName}'.",
            request.Method,
            request.RequestUri,
            callerServiceName);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Remote queryable '{ServiceName}' returned {StatusCode} ({StatusName}) for {Method} {Url}.",
                callerServiceName,
                (int)response.StatusCode,
                response.StatusCode,
                request.Method,
                request.RequestUri);
        }

        return response;
    }

    private async Task<IReadOnlyList<QueryableSourceResponse>> EnsureRegistryAsync(
        CancellationToken cancellationToken) =>
        [.. (await remoteRegistry.GetAsync(clientName, callerServiceName, registryTtl, headerStamper, cancellationToken)).Queryables];
}

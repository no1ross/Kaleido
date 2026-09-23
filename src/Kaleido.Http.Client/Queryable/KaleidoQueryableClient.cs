using System.Net;
using System.Net.Http.Json;
using Kaleido.Http.Queryable.Contracts;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Queryable;

internal sealed class KaleidoQueryableClient(
    HttpClient httpClient,
    ICorrelationHeaderStamper headerStamper,
    ILogger<KaleidoQueryableClient> logger,
    string callerServiceName = "")
    : IKaleidoQueryableClient, IDisposable
{
    private readonly SemaphoreSlim _registryLock = new(1, 1);
    private IReadOnlyList<QueryableRecordResponse>? _registry;

    public void Dispose()
    {
        _registryLock.Dispose();
    }

    public async Task<IReadOnlyList<QueryableRecordResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default)
    {
        return await EnsureRegistryAsync(cancellationToken);
    }

    public async Task<QueryableRecordResponse> GetContextMetadataAsync(
        string context,
        CancellationToken cancellationToken = default)
    {
        var registry = await EnsureRegistryAsync(cancellationToken);

        var contextRecord = registry.FirstOrDefault(
            r => string.Equals(r.Name, context, StringComparison.OrdinalIgnoreCase))
            ?? throw new KaleidoHttpClientException(
                HttpClientErrorCodes.NotFound,
                $"{callerServiceName} tried to call context '{context}' on the remote registry, but it was not found.",
                HttpStatusCode.NotFound);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, contextRecord.MetadataUrl);

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<QueryableRecordResponse>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       $"{callerServiceName} tried to call context '{context}' metadata, but the request succeeded and returned no payload.",
                       response.StatusCode);
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            $"{callerServiceName} tried to call context '{context}' metadata, but the request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    public async Task<QueryResult<TView>> QueryViewAsync<TParameters, TView>(
        string context,
        string view,
        QueryApiRequest<TParameters> request,
        CancellationToken cancellationToken = default)
        where TParameters : class
        where TView : class
    {
        var registry = await EnsureRegistryAsync(cancellationToken);

        var contextRecord = registry.FirstOrDefault(
            r => string.Equals(r.Name, context, StringComparison.OrdinalIgnoreCase))
            ?? throw new KaleidoHttpClientException(
                HttpClientErrorCodes.NotFound,
                $"{callerServiceName} tried to call view '{view}' on context '{context}', but the context was not found in the remote registry.",
                HttpStatusCode.NotFound);

        var viewRecord = contextRecord.Views.FirstOrDefault(
            v => string.Equals(v.Name, view, StringComparison.OrdinalIgnoreCase))
            ?? throw new KaleidoHttpClientException(
                HttpClientErrorCodes.NotFound,
                $"{callerServiceName} tried to call view '{view}' on context '{context}', but the view was not found in the remote registry.",
                HttpStatusCode.NotFound);

        return await SendQueryAsync<TView>(viewRecord.QueryUrl, request, cancellationToken, context, view);
    }

    public async Task<QueryResult<TView>> QueryContextAsync<TView>(
        string context,
        QueryApiRequest request,
        CancellationToken cancellationToken = default)
        where TView : class
    {
        var registry = await EnsureRegistryAsync(cancellationToken);

        var contextRecord = registry.FirstOrDefault(
            r => string.Equals(r.Name, context, StringComparison.OrdinalIgnoreCase))
            ?? throw new KaleidoHttpClientException(
                HttpClientErrorCodes.NotFound,
                $"{callerServiceName} tried to call context '{context}', but the context was not found in the remote registry.",
                HttpStatusCode.NotFound);

        if (string.IsNullOrEmpty(contextRecord.QueryUrl))
        {
            throw new KaleidoHttpClientException(
                HttpClientErrorCodes.NotFound,
                $"{callerServiceName} tried to call context '{context}', but the context does not support direct queries (no QueryUrl). Only Direct contexts expose a query URL.",
                HttpStatusCode.NotFound);
        }

        return await SendQueryAsync<TView>(contextRecord.QueryUrl, request, cancellationToken, context, null);
    }

    private async Task<QueryResult<TView>> SendQueryAsync<TView>(
        string url,
        object request,
        CancellationToken cancellationToken,
        string? context = null,
        string? view = null)
        where TView : class
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        headerStamper.Stamp(httpRequest);

        using var response = await SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<QueryResult<TView>>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoHttpClientException(
                       HttpClientErrorCodes.EmptyResponse,
                       $"{callerServiceName} tried to call {FormatTarget(context, view)}, but the request succeeded and returned no payload.",
                       response.StatusCode);
        }

        var errorResponse =
            response.StatusCode == HttpStatusCode.BadRequest
                ? await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(
                    cancellationToken: cancellationToken)
                : null;

        if (errorResponse?.Errors.Count > 0)
        {
            throw new KaleidoHttpClientException(
                HttpClientErrorCodes.ValidationFailed,
                $"{callerServiceName} tried to call {FormatTarget(context, view)}, but the request failed: {string.Join(" ", errorResponse.Errors.Select(e => e.Message))}",
                response.StatusCode,
                errorResponse.Errors);
        }

        throw new KaleidoHttpClientException(
            HttpClientErrorCodes.RequestFailed,
            $"{callerServiceName} tried to call {FormatTarget(context, view)}, but the request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
    }

    private static string FormatTarget(string? context, string? view)
    {
        if (!string.IsNullOrEmpty(view))
        {
            return $"view '{view}' on context '{context}'";
        }

        return $"context '{context}'";
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

    private async Task<IReadOnlyList<QueryableRecordResponse>> EnsureRegistryAsync(
        CancellationToken cancellationToken)
    {
        if (_registry is not null)
        {
            return _registry;
        }

        await _registryLock.WaitAsync(cancellationToken);
        try
        {
            if (_registry is not null)
            {
                return _registry;
            }

            using var registryRequest = new HttpRequestMessage(HttpMethod.Get, QueryableContractUrls.QueryRegistry(callerServiceName));
            headerStamper.Stamp(registryRequest);
            using var registryResponse = await SendAsync(registryRequest, cancellationToken);

            var registry = await registryResponse.Content.ReadFromJsonAsync<IReadOnlyList<QueryableRecordResponse>>(
                cancellationToken: cancellationToken)
                ?? throw new KaleidoHttpClientException(
                    HttpClientErrorCodes.EmptyResponse,
                    $"{callerServiceName} tried to call the queryable registry, but the request succeeded and returned no payload.",
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

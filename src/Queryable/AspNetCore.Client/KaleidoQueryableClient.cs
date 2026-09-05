using Kaleido.Observability;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Query;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Queryable.AspNetCore.Client;

internal sealed class KaleidoQueryableClient : IKaleidoQueryableClient
{
    private readonly HttpClient _httpClient;
    private readonly IKaleidoCorrelationContextAccessor _correlation;
    private readonly SemaphoreSlim _registryLock = new(1, 1);
    private IReadOnlyList<QueryableRecordResponse>? _registry;

    public KaleidoQueryableClient(
        HttpClient httpClient,
        IKaleidoCorrelationContextAccessor correlation)
    {
        _httpClient = httpClient;
        _correlation = correlation;
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
            ?? throw new InvalidOperationException(
                $"Queryable context '{context}' was not found in the remote registry.");

        var viewRecord = contextRecord.Views.FirstOrDefault(
            v => string.Equals(v.Name, view, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"View '{view}' was not found on context '{context}' in the remote registry.");

        return await SendQueryAsync<TView>(viewRecord.QueryUrl, request, cancellationToken);
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
            ?? throw new InvalidOperationException(
                $"Queryable context '{context}' was not found in the remote registry.");

        if (string.IsNullOrEmpty(contextRecord.QueryUrl))
        {
            throw new InvalidOperationException(
                $"Queryable context '{context}' does not support direct queries (no QueryUrl). Only Direct contexts expose a query URL.");
        }

        return await SendQueryAsync<TView>(contextRecord.QueryUrl, request, cancellationToken);
    }

    private async Task<QueryResult<TView>> SendQueryAsync<TView>(
        string url,
        object request,
        CancellationToken cancellationToken)
        where TView : class
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        StampCorrelationHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<QueryResult<TView>>(
                       cancellationToken: cancellationToken)
                   ?? throw new KaleidoQueryableClientException(
                       "Queryable request succeeded but returned no payload.",
                       response.StatusCode);
        }

        var errorResponse =
            response.StatusCode == HttpStatusCode.BadRequest
                ? await response.Content.ReadFromJsonAsync<QueryErrorResponse>(
                    cancellationToken: cancellationToken)
                : null;

        if (errorResponse?.Errors.Count > 0)
        {
            throw new KaleidoQueryableClientException(
                string.Join(" ", errorResponse.Errors.Select(e => e.Message)),
                response.StatusCode,
                errorResponse.Errors);
        }

        throw new KaleidoQueryableClientException(
            $"Queryable request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode);
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

    private async Task<IReadOnlyList<QueryableRecordResponse>> EnsureRegistryAsync(
        CancellationToken cancellationToken)
    {
        if (_registry is not null)
            return _registry;

        await _registryLock.WaitAsync(cancellationToken);
        try
        {
            if (_registry is not null)
                return _registry;

            var registry = await _httpClient.GetFromJsonAsync<IReadOnlyList<QueryableRecordResponse>>(
                "/queryable/registry",
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Queryable registry request succeeded but returned no payload.");

            _registry = registry;
            return _registry;
        }
        finally
        {
            _registryLock.Release();
        }
    }
}

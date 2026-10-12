using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

/// <summary>
/// Singleton remote-registry fetcher shared by all typed clients and the
/// aggregate registry endpoint. One HTTP call per named client — the returned
/// <see cref="AggregatedRegistryResponse"/> carries both process and queryable
/// registrations, cached once per client name.
/// </summary>
internal sealed partial class KaleidoRemoteRegistry(
    IHttpClientFactory httpClientFactory,
    IRegistrySnapshotStore snapshotStore,
    ILogger<KaleidoRemoteRegistry> logger)
{
    private readonly ConcurrentDictionary<string, HttpClientRegistryCache> _caches =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<AggregatedRegistryResponse> GetAsync(
        string clientName,
        string routePrefix,
        TimeSpan? registryTtl,
        ICorrelationHeaderStamper stamper,
        CancellationToken cancellationToken) =>
        _caches.GetOrAdd(
                clientName,
                // Canonical per-service key — matches the key the downstream
                // writes its own snapshot under (kaleido:{ServiceName}), since
                // RoutePrefix equals ServiceName by contract.
                static (_, state) => new HttpClientRegistryCache(state.SnapshotStore, $"kaleido:{state.RoutePrefix}"),
                (SnapshotStore: snapshotStore, RoutePrefix: routePrefix))
            .GetOrRevalidateAsync(
                registryTtl,
                (revision, ct) => FetchAsync(clientName, routePrefix, stamper, revision, ct),
                cancellationToken);

    public void Invalidate(string? clientName = null)
    {
        if (clientName is null)
        {
            foreach (var entry in _caches.Values)
            {
                entry.Reset();
            }

            return;
        }

        if (_caches.TryGetValue(clientName, out var cache))
        {
            cache.Reset();
        }
    }

    // The remote's registry, or null when it answers that the known revision is still current (304).
    private async Task<AggregatedRegistryResponse?> FetchAsync(
        string clientName,
        string routePrefix,
        ICorrelationHeaderStamper stamper,
        string? knownRevision,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            RegistryContractUrls.Registry(routePrefix));
        stamper.Stamp(request);

        if (knownRevision is not null)
        {
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{knownRevision}\""));
        }

        // IHttpClientFactory owns the client lifetime — never dispose it.
        var httpClient = httpClientFactory.CreateClient(clientName);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (knownRevision is not null && response.StatusCode == HttpStatusCode.NotModified)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            LogRegistryFetchFailed(logger, clientName, (int)response.StatusCode, response.StatusCode);

            throw new KaleidoHttpClientException(
                HttpClientErrorCodes.RequestFailed,
                string.Create(CultureInfo.InvariantCulture, $"Registry request to client '{clientName}' failed with status code {(int)response.StatusCode} ({response.StatusCode})."),
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(
                   KaleidoJsonOptions.Options,
                   cancellationToken)
               ?? throw new KaleidoHttpClientException(
                   HttpClientErrorCodes.EmptyResponse,
                   $"Registry request to client '{clientName}' succeeded but returned no payload.",
                   HttpStatusCode.InternalServerError);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registry fetch from client '{ClientName}' failed with status code {StatusCode} ({StatusName}).")]
    private static partial void LogRegistryFetchFailed(ILogger logger, string clientName, int statusCode, HttpStatusCode statusName);
}

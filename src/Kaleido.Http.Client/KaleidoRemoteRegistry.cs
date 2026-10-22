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
/// <remarks>
/// A remote registry is filtered for the caller that fetched it, so this cache is used only for
/// discovery and aggregation (<c>GetRegistryAsync</c>), never to route capability calls, and it is
/// stored under its own key, <c>kaleido-client:{ServiceName}:{clientName}</c> with this service's own
/// name: never under the remote service's <c>kaleido:{ServiceName}</c>, where that service keeps its
/// unfiltered snapshot (#381), and never shared with another consuming service, whose copy is
/// filtered for a different caller (#275).
/// </remarks>
internal sealed partial class KaleidoRemoteRegistry(
    IHttpClientFactory httpClientFactory,
    IRegistrySnapshotStore snapshotStore,
    KaleidoServiceOptions serviceOptions,
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
                static (name, state) => new HttpClientRegistryCache(state.Store, CacheKey(state.ServiceName, name)),
                (Store: snapshotStore, serviceOptions.ServiceName))
            .GetOrRevalidateAsync(
                registryTtl,
                (revision, ct) => FetchAsync(clientName, routePrefix, stamper, revision, ct),
                cancellationToken);

    // The client's own key, scoped to this (consuming) service: in a shared store a caller-filtered
    // copy must never replace the remote service's unfiltered snapshot (kaleido:{ServiceName}), nor
    // be read by another service, whose copy is filtered for a different caller.
    internal static string CacheKey(string serviceName, string clientName) => $"kaleido-client:{serviceName}:{clientName}";

    public async Task InvalidateAsync(
        string clientName,
        CancellationToken cancellationToken)
    {
        if (_caches.TryGetValue(clientName, out var cache))
        {
            await cache.ResetAsync(cancellationToken);
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

        // Shared by every caller (cached, filtered per caller at the edge): the service's own call.
        KaleidoCallContext.MarkServiceOwn(request);

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

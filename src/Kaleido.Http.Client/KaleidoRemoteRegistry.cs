using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

/// <summary>
/// Singleton remote-registry fetcher shared by all typed clients and the
/// aggregate registry endpoint. One HTTP call per named client — the returned
/// <see cref="AggregatedRegistryResponse"/> carries both process and queryable
/// registrations, cached once per client name.
/// </summary>
internal sealed class KaleidoRemoteRegistry(
    IHttpClientFactory httpClientFactory,
    ILogger<KaleidoRemoteRegistry> logger)
{
    private readonly ConcurrentDictionary<string, HttpClientRegistryCache<AggregatedRegistryResponse>> _caches =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<AggregatedRegistryResponse> GetAsync(
        string clientName,
        string routePrefix,
        ICorrelationHeaderStamper stamper,
        CancellationToken cancellationToken) =>
        _caches.GetOrAdd(clientName, _ => new HttpClientRegistryCache<AggregatedRegistryResponse>())
            .GetOrFetchAsync(
                ct => FetchAsync(clientName, routePrefix, stamper, ct),
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

    private async Task<AggregatedRegistryResponse> FetchAsync(
        string clientName,
        string routePrefix,
        ICorrelationHeaderStamper stamper,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            RegistryContractUrls.Registry(routePrefix));
        stamper.Stamp(request);

        // IHttpClientFactory owns the client lifetime — never dispose it.
        var httpClient = httpClientFactory.CreateClient(clientName);
        var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Registry fetch from client '{ClientName}' failed with status code {StatusCode} ({StatusName}).",
                clientName,
                (int)response.StatusCode,
                response.StatusCode);

            throw new KaleidoHttpClientException(
                HttpClientErrorCodes.RequestFailed,
                $"Registry request to client '{clientName}' failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
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
}

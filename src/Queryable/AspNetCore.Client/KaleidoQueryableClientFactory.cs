using Kaleido.Observability;
using System.Linq;

namespace Kaleido.Queryable.AspNetCore.Client;

internal sealed class KaleidoQueryableClientFactory : IKaleidoQueryableClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKaleidoCorrelationContextAccessor _correlation;
    private readonly KaleidoQueryableClientRouteOptionsMap _routeOptionsMap;
    private readonly Dictionary<string, IKaleidoQueryableClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public KaleidoQueryableClientFactory(
        IHttpClientFactory httpClientFactory,
        IKaleidoCorrelationContextAccessor correlation,
        KaleidoQueryableClientRouteOptionsMap routeOptionsMap)
    {
        _httpClientFactory = httpClientFactory;
        _correlation = correlation;
        _routeOptionsMap = routeOptionsMap;
    }

    public IKaleidoQueryableClient GetClient(string name)
    {
        if (_clients.TryGetValue(name, out var existing))
            return existing;

        lock (_lock)
        {
            if (_clients.TryGetValue(name, out existing))
                return existing;

            _routeOptionsMap.Options.TryGetValue(name, out var serviceName);

            // Find the exact registered name (case-sensitive) from the map.
            // This handles the case where handlers call GetClient with lowercase
            // but HttpClients are registered with PascalCase.
            var registeredName = _routeOptionsMap.Options.Keys.FirstOrDefault(k =>
                string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) ?? name;

            var httpClient = _httpClientFactory.CreateClient(registeredName);
            var client = new KaleidoQueryableClient(httpClient, _correlation, serviceName ?? "");
            _clients[name] = client;
            return client;
        }
    }
}

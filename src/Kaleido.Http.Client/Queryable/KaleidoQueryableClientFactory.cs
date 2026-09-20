

namespace Kaleido.Http.Client.Queryable;

internal sealed class KaleidoQueryableClientFactory(
    IHttpClientFactory httpClientFactory,
    IKaleidoCorrelationContextAccessor correlation,
    KaleidoQueryableClientRouteOptionsMap routeOptionsMap)
    : IKaleidoQueryableClientFactory
{
    private readonly Dictionary<string, IKaleidoQueryableClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public IKaleidoQueryableClient GetClient(string name)
    {
        if (_clients.TryGetValue(name, out var existing))
            return existing;

        lock (_lock)
        {
            if (_clients.TryGetValue(name, out existing))
                return existing;

            routeOptionsMap.Options.TryGetValue(name, out var serviceName);

            // Find the exact registered name (case-sensitive) from the map.
            // This handles the case where handlers call GetClient with lowercase
            // but HttpClients are registered with PascalCase.
            var registeredName = routeOptionsMap.Options.Keys.FirstOrDefault(k =>
                string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) ?? name;

            var httpClient = httpClientFactory.CreateClient(registeredName);
            var client = new KaleidoQueryableClient(httpClient, correlation, serviceName ?? "");
            _clients[name] = client;
            return client;
        }
    }
}

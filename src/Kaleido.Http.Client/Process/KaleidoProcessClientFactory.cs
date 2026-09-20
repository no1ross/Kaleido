

namespace Kaleido.Http.Client.Process;

internal sealed class KaleidoProcessClientFactory(
    IHttpClientFactory httpClientFactory,
    IKaleidoCorrelationContextAccessor correlation,
    KaleidoProcessClientRouteOptionsMap routeOptionsMap)
    : IKaleidoProcessClientFactory
{
    private readonly Dictionary<string, IKaleidoProcessClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public IKaleidoProcessClient GetClient(string name)
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
            var client = new KaleidoProcessClient(httpClient, correlation, serviceName ?? "");
            _clients[name] = client;
            return client;
        }
    }
}

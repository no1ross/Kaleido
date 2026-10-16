using Kaleido.Exceptions;

namespace Kaleido.Http.Client;

internal abstract class KaleidoClientFactoryBase<TClient, TMap>
    where TClient : class
    where TMap : class, IKaleidoClientRouteOptionsMap
{
    protected abstract IHttpClientFactory HttpClientFactory { get; }
    protected abstract ICorrelationHeaderStamper HeaderStamper { get; }
    protected abstract TMap RouteOptionsMap { get; }

    protected abstract TClient CreateClient(
        System.Net.Http.HttpClient httpClient,
        ICorrelationHeaderStamper headerStamper,
        string clientName,
        string serviceName,
        TimeSpan? registryTtl);

    public TClient GetClient(string name)
    {
        // Find the exact registered name (case-sensitive) from the map.
        // This handles the case where handlers call GetClient with lowercase
        // but HttpClients are registered with PascalCase.
        var registeredName = GetRegisteredName(name)
            ?? throw new KaleidoConfigurationException(
                HttpClientErrorCodes.UnknownClient,
                $"Kaleido client '{name}' is not configured under Kaleido:Clients; {ConfiguredClients()}.");

        var httpClient = HttpClientFactory.CreateClient(registeredName);
        return CreateClient(httpClient, HeaderStamper, registeredName, RouteOptionsMap.Options[registeredName], GetRegistryTtl(registeredName));
    }

    private string ConfiguredClients() =>
        RouteOptionsMap.Options.Count == 0
            ? "no clients are configured"
            : "configured clients: " + string.Join(", ", RouteOptionsMap.Options.Keys.Order(StringComparer.OrdinalIgnoreCase));

    public bool TryGetClientForService(
        string serviceName,
        [NotNullWhen(true)] out TClient? client)
    {
        var name =
            RouteOptionsMap.Options
                .FirstOrDefault(x => string.Equals(x.Value, serviceName, StringComparison.OrdinalIgnoreCase))
                .Key;

        client = name is null ? null : GetClient(name);

        return client is not null;
    }

    private TimeSpan? GetRegistryTtl(string name) =>
        RouteOptionsMap.RegistryTtls.TryGetValue(name, out var ttl)
            ? ttl
            : null;

    private string? GetRegisteredName(string name) =>
        RouteOptionsMap.Options.Keys.FirstOrDefault(k =>
            string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
}

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
        string serviceName);

    public TClient GetClient(string name)
    {
        var serviceName = GetServiceName(name);

        // Find the exact registered name (case-sensitive) from the map.
        // This handles the case where handlers call GetClient with lowercase
        // but HttpClients are registered with PascalCase.
        var registeredName = GetRegisteredName(name) ?? name;

        var httpClient = HttpClientFactory.CreateClient(registeredName);
        return CreateClient(httpClient, HeaderStamper, serviceName);
    }

    private string GetServiceName(string name) =>
        RouteOptionsMap.Options.TryGetValue(name, out var serviceName)
            ? serviceName
            : string.Empty;

    private string? GetRegisteredName(string name) =>
        RouteOptionsMap.Options.Keys.FirstOrDefault(k =>
            string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
}

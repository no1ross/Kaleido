namespace Kaleido.Http.Client;

internal abstract class KaleidoClientFactoryBase<TClient, TMap>
    where TClient : class
    where TMap : class
{
    private readonly Dictionary<string, TClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    protected abstract IHttpClientFactory HttpClientFactory { get; }
    protected abstract IKaleidoCorrelationContextAccessor CorrelationAccessor { get; }
    protected abstract ICorrelationHeaderStamper HeaderStamper { get; }
    protected abstract TMap RouteOptionsMap { get; }

    protected abstract TClient CreateClient(
        System.Net.Http.HttpClient httpClient,
        ICorrelationHeaderStamper headerStamper,
        string serviceName);

    public TClient GetClient(string name)
    {
        if (_clients.TryGetValue(name, out var existing))
        {
            return existing;
        }

        lock (_lock)
        {
            if (_clients.TryGetValue(name, out existing))
            {
                return existing;
            }

            var serviceName = GetServiceName(name);

            // Find the exact registered name (case-sensitive) from the map.
            // This handles the case where handlers call GetClient with lowercase
            // but HttpClients are registered with PascalCase.
            var registeredName = GetRegisteredName(name) ?? name;

            var httpClient = HttpClientFactory.CreateClient(registeredName);
            var client = CreateClient(httpClient, HeaderStamper, serviceName);
            _clients[name] = client;
            return client;
        }
    }

    private string GetServiceName(string name)
    {
        var optionsMap = GetOptionsMap();
        if (optionsMap.TryGetValue(name, out var serviceName))
        {
            return serviceName;
        }

        return string.Empty;
    }

    private Dictionary<string, string> GetOptionsMap()
    {
        var map = RouteOptionsMap;
        var optionsProperty = map.GetType().GetProperty("Options");
        if (optionsProperty == null)
        {
            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                "Route options map does not have 'Options' property.");
        }

        return (Dictionary<string, string>)(optionsProperty.GetValue(map)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                "Route options map 'Options' property returned null."));
    }

    private string? GetRegisteredName(string name)
    {
        var optionsMap = GetOptionsMap();
        return optionsMap.Keys.FirstOrDefault(k =>
            string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
    }
}

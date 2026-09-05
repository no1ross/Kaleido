using Kaleido.Observability;

namespace Kaleido.Process.AspNetCore.Client;

internal sealed class KaleidoProcessClientFactory : IKaleidoProcessClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKaleidoCorrelationContextAccessor _correlation;
    private readonly Dictionary<string, IKaleidoProcessClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public KaleidoProcessClientFactory(
        IHttpClientFactory httpClientFactory,
        IKaleidoCorrelationContextAccessor correlation)
    {
        _httpClientFactory = httpClientFactory;
        _correlation = correlation;
    }

    public IKaleidoProcessClient GetClient(string name)
    {
        if (_clients.TryGetValue(name, out var existing))
            return existing;

        lock (_lock)
        {
            if (_clients.TryGetValue(name, out existing))
                return existing;

            var httpClient = _httpClientFactory.CreateClient(name);
            var client = new KaleidoProcessClient(httpClient, _correlation);
            _clients[name] = client;
            return client;
        }
    }
}

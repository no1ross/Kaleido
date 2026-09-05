using Kaleido.Observability;

namespace Kaleido.Queryable.AspNetCore.Client;

internal sealed class KaleidoQueryableClientFactory : IKaleidoQueryableClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKaleidoCorrelationContextAccessor _correlation;
    private readonly Dictionary<string, IKaleidoQueryableClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public KaleidoQueryableClientFactory(
        IHttpClientFactory httpClientFactory,
        IKaleidoCorrelationContextAccessor correlation)
    {
        _httpClientFactory = httpClientFactory;
        _correlation = correlation;
    }

    public IKaleidoQueryableClient GetClient(string name)
    {
        if (_clients.TryGetValue(name, out var existing))
            return existing;

        lock (_lock)
        {
            if (_clients.TryGetValue(name, out existing))
                return existing;

            var httpClient = _httpClientFactory.CreateClient(name);
            var client = new KaleidoQueryableClient(httpClient, _correlation);
            _clients[name] = client;
            return client;
        }
    }
}

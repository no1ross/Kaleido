namespace Kaleido.Http;

/// <summary>
/// The Kaleido clients configured for this host (<c>Kaleido:Clients</c>), by name: the remote
/// service each one calls and its registry settings. Filled at registration by <c>AddHttpClients()</c>
/// from one <see cref="KaleidoClientDefinition"/> per client; read by the clients, their factories,
/// the registry aggregation and the per-client health checks and credential handlers (#498).
/// </summary>
internal interface IKaleidoClientCatalog
{
    /// <summary>Every configured client.</summary>
    IReadOnlyCollection<KaleidoClientDefinition> Clients { get; }

    /// <summary>The client registered under <paramref name="name"/> (case-insensitive), or null.</summary>
    KaleidoClientDefinition? Find(string name);

    /// <summary>The client that calls the service <paramref name="serviceName"/> (its route prefix), or null.</summary>
    KaleidoClientDefinition? FindByService(string serviceName);
}

internal sealed class KaleidoClientCatalog(IEnumerable<KaleidoClientDefinition> definitions) : IKaleidoClientCatalog
{
    private readonly Dictionary<string, KaleidoClientDefinition> _byName =
        definitions
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<KaleidoClientDefinition> Clients => _byName.Values;

    public KaleidoClientDefinition? Find(string name) =>
        _byName.TryGetValue(name, out var client) ? client : null;

    public KaleidoClientDefinition? FindByService(string serviceName) =>
        _byName.Values.FirstOrDefault(x => string.Equals(x.RoutePrefix, serviceName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>One configured Kaleido client: the settings <c>AddHttpClients()</c> resolved for it.</summary>
/// <param name="Name">The client name (the key under <c>Kaleido:Clients</c>), also the name of its <c>HttpClient</c>.</param>
/// <param name="RoutePrefix">The remote service's name, the first segment of its routes.</param>
/// <param name="RegistryTtl">How often the remote registry is checked again; null = until invalidated.</param>
/// <param name="StrictRegistryProbe">The health check probes <c>?strict</c>.</param>
/// <param name="HealthCheckTimeout">The health check's own timeout.</param>
internal sealed record KaleidoClientDefinition(
    string Name,
    string RoutePrefix,
    TimeSpan? RegistryTtl,
    bool StrictRegistryProbe,
    TimeSpan HealthCheckTimeout);

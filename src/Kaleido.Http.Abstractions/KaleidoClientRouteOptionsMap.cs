namespace Kaleido.Http;

/// <summary>
/// Contract for the DI-singleton that holds the name-to-routePrefix map for
/// a named Kaleido HTTP client. Used as a type constraint on TMap in
/// KaleidoClientFactoryBase and KaleidoClientServiceCollectionExtensions — eliminates reflection
/// over the Options property. The registration extension writes entries at startup
/// (<see cref="SetRoutePrefix"/>, <see cref="SetRegistryTtl"/>); consumers read the maps at request time.
/// </summary>
internal interface IKaleidoClientRouteOptionsMap
{
    /// <summary>Client name → the remote service's route prefix.</summary>
    IReadOnlyDictionary<string, string> Options { get; }

    /// <summary>
    /// Per-client registry-cache TTL written at registration time (only
    /// entries with a configured TTL appear). Read by the client factories so
    /// every client instance shares the registered freshness window.
    /// </summary>
    IReadOnlyDictionary<string, TimeSpan> RegistryTtls { get; }

    /// <summary>Registers (or replaces) the route prefix of a client.</summary>
    void SetRoutePrefix(string clientName, string routePrefix);

    /// <summary>Registers (or replaces) the registry-cache TTL of a client.</summary>
    void SetRegistryTtl(string clientName, TimeSpan ttl);
}

/// <summary>
/// Base implementation of <see cref="IKaleidoClientRouteOptionsMap"/>.
/// Subclassed once per client type so each registers as a distinct DI singleton
/// without key collision.
/// </summary>
internal abstract class KaleidoClientRouteOptionsMap : IKaleidoClientRouteOptionsMap
{
    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TimeSpan> _registryTtls = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Options => _options;

    public IReadOnlyDictionary<string, TimeSpan> RegistryTtls => _registryTtls;

    public void SetRoutePrefix(string clientName, string routePrefix) =>
        _options[clientName] = routePrefix;

    public void SetRegistryTtl(string clientName, TimeSpan ttl) =>
        _registryTtls[clientName] = ttl;
}

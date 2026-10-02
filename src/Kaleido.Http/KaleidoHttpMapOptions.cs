namespace Kaleido.Http;

/// <summary>
/// Options for <see cref="KaleidoEndpointRouteBuilderExtensions.MapKaleidoHttp"/>.
/// </summary>
public sealed class KaleidoHttpMapOptions
{
    /// <summary>
    /// When true, the registry endpoint aggregates downstream services:
    /// every client registered via <c>AddHttpClients()</c> is fetched and
    /// merged into this service's response. Requires <c>AddHttpClients()</c>.
    /// When false (default), <c>GET /{service}/registry</c> returns only this
    /// service's local registrations.
    /// </summary>
    public bool AggregateRegistry { get; set; }

    /// <summary>
    /// Maximum age of the server-side registry snapshot. A cached response
    /// older than this is rebuilt on the next request — this also bounds how
    /// long a clean snapshot can mask a downstream that started failing.
    /// When null (default), the snapshot never expires on its own; use the
    /// <c>?refresh</c> query parameter to force a rebuild.
    /// </summary>
    public TimeSpan? RegistryCacheTtl { get; set; }

    /// <summary>
    /// Minimum interval between honored <c>?refresh</c> requests, measured from
    /// the snapshot's build time. Refresh requests inside the cooldown are
    /// served the cached snapshot instead of triggering another downstream
    /// fan-out. Defaults to 30 seconds; <see cref="TimeSpan.Zero"/> disables
    /// throttling.
    /// </summary>
    public TimeSpan RegistryRefreshCooldown { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of downstream registry fetches in flight at once during
    /// an aggregate build/rebuild. When null (default) every registered client
    /// is fetched concurrently — acceptable for a handful of downstreams, but
    /// set this when a router aggregates many services so one refresh cannot
    /// open a connection to every downstream simultaneously.
    /// </summary>
    public int? RegistryDownstreamMaxParallelism { get; set; }
}

namespace Kaleido.Http;

/// <summary>
/// Options for <see cref="KaleidoEndpointRouteBuilderExtensions.MapKaleidoHttp"/>.
/// </summary>
/// <remarks>
/// Immutable: <c>MapKaleidoHttp</c> lays the <c>Kaleido:Http:Map</c> configuration section over a copy
/// of the instance passed in code (configuration overrides code, setting by setting) and validates it
/// (ADR 0030).
/// </remarks>
public sealed record KaleidoHttpMapOptions
{
    /// <summary>The configuration section that overrides these options: <c>Kaleido:Http:Map</c>.</summary>
    public const string SectionName = "Kaleido:Http:Map";

    /// <summary>
    /// When true, the registry endpoint aggregates downstream services:
    /// every client registered via <c>AddHttpClients()</c> is fetched and
    /// merged into this service's response. Requires <c>AddHttpClients()</c>.
    /// When false (default), <c>GET /{service}/registry</c> returns only this
    /// service's local registrations.
    /// </summary>
    public bool AggregateRegistry { get; init; }

    /// <summary>
    /// How long the server-side registry snapshot is served before it is rebuilt on the next request.
    /// Default 30 seconds.
    /// </summary>
    /// <remarks>
    /// On an aggregating host (a router) the rebuild checks every downstream with its ETag, so an
    /// unchanged downstream costs one small request with an empty 304 answer; a deployed one is picked
    /// up. This is how a router, and the MCP tool list built from it, learn about deployments without a
    /// restart. It also bounds how long a clean snapshot can mask a downstream that started failing.
    /// Null means the snapshot never expires on its own; use <c>?refresh</c> to force a rebuild.
    /// </remarks>
    public TimeSpan? RegistryCacheTtl { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Minimum interval between honored <c>?refresh</c> requests, measured from
    /// the snapshot's build time. Refresh requests inside the cooldown are
    /// served the cached snapshot instead of triggering another downstream
    /// fan-out. Defaults to 30 seconds; <see cref="TimeSpan.Zero"/> disables
    /// throttling.
    /// </summary>
    public TimeSpan RegistryRefreshCooldown { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of downstream registry fetches in flight at once during
    /// an aggregate build/rebuild. When null (default) every registered client
    /// is fetched concurrently — acceptable for a handful of downstreams, but
    /// set this when a router aggregates many services so one refresh cannot
    /// open a connection to every downstream simultaneously.
    /// </summary>
    public int? RegistryDownstreamMaxParallelism { get; init; }

    // Run by MapKaleidoHttp after configuration is laid over the options; nothing is adjusted, an
    // invalid value stops startup.
    internal static void Validate(KaleidoHttpMapOptions options)
    {
        if (options.RegistryCacheTtl < TimeSpan.Zero)
        {
            throw Invalid(nameof(RegistryCacheTtl), options.RegistryCacheTtl, "zero or more when set (zero rebuilds the snapshot on every request)");
        }

        if (options.RegistryRefreshCooldown < TimeSpan.Zero)
        {
            throw Invalid(nameof(RegistryRefreshCooldown), options.RegistryRefreshCooldown, "zero or more");
        }

        if (options.RegistryDownstreamMaxParallelism <= 0)
        {
            throw Invalid(nameof(RegistryDownstreamMaxParallelism), options.RegistryDownstreamMaxParallelism, "positive when set");
        }
    }

    private static KaleidoConfigurationException Invalid(string name, object? value, string rule) =>
        new(ConfigurationErrorCodes.InvalidHttpOptions, $"{SectionName}:{name} is {value}; it must be {rule}.");
}

namespace Kaleido.Http.Registry;

/// <summary>
/// The host's shared registry snapshot: the unfiltered union of local and downstream registrations,
/// cached once for all callers (canonical key <c>kaleido:{ServiceName}</c>), plus the settings that
/// <c>MapKaleidoHttp</c> decides for it (aggregation, staleness, refresh throttling, fan-out bound).
/// </summary>
/// <remarks>
/// <see cref="Configure"/> runs once at startup, when the registry endpoint is mapped and before
/// requests are served; afterwards the settings are only read.
/// </remarks>
internal sealed class RegistrySnapshot(
    IRegistrySnapshotStore store,
    KaleidoServiceOptions serviceOptions)
    : IDisposable
{
    private readonly HttpRegistryCache _cache = new(store, $"kaleido:{serviceOptions.ServiceName}");

    /// <summary>True once the registry endpoint is mapped (and the settings decided).</summary>
    public bool IsMapped { get; private set; }

    /// <summary>Downstream clients' registries are merged in (a router or gateway).</summary>
    public bool Aggregate { get; private set; }

    /// <summary>The maximum age of the snapshot (null = until refreshed).</summary>
    public TimeSpan? CacheTtl { get; private set; }

    /// <summary>The minimum interval between honored refreshes.</summary>
    public TimeSpan RefreshCooldown { get; private set; } = TimeSpan.FromSeconds(30);

    /// <summary>At most this many downstream fetches in flight (null = all at once).</summary>
    public int? DownstreamMaxParallelism { get; private set; }

    public void Configure(KaleidoHttpMapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Aggregate = options.AggregateRegistry;
        CacheTtl = options.RegistryCacheTtl;
        RefreshCooldown = options.RegistryRefreshCooldown;
        DownstreamMaxParallelism = options.RegistryDownstreamMaxParallelism;
        IsMapped = true;
    }

    /// <summary>
    /// The snapshot while fresh (and not force-refreshed outside the cooldown); otherwise
    /// <paramref name="build"/> rebuilds it. Only clean (non-partial) snapshots are stored.
    /// </summary>
    public Task<AggregatedRegistryResponse> GetOrBuildAsync(
        bool refresh,
        Func<CancellationToken, Task<AggregatedRegistryResponse>> build,
        CancellationToken cancellationToken) =>
        _cache.GetOrBuildAsync(CacheTtl, RefreshCooldown, refresh, build, cancellationToken);

    public void Dispose() => _cache.Dispose();
}

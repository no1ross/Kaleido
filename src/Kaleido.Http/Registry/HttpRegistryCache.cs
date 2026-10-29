namespace Kaleido.Http.Registry;

/// <summary>
/// Server-side cache for the aggregated registry response, backed by
/// <see cref="IRegistrySnapshotStore"/> (in-memory by default; aggregating
/// hosts may register a distributed implementation). Only stores
/// fully-successful (zero <see cref="AggregatedRegistryResponse.ClientErrors"/>)
/// results. Partial results are served to callers but never committed to the
/// store, so the last clean snapshot remains available for subsequent calls —
/// and every request while degraded naturally retries the failed downstreams.
/// </summary>
internal interface IHttpRegistryCache
{
    /// <summary>
    /// The snapshot of <paramref name="key"/> while it is fresh: younger than <paramref name="ttl"/>
    /// (null = never expires) and not force-refreshed - except that refresh requests inside the
    /// <paramref name="refreshCooldown"/> window are throttled back to the cache. Otherwise invokes
    /// <paramref name="build"/>, stamps <see cref="AggregatedRegistryResponse.GeneratedAt"/>/<c>IsPartial</c>,
    /// and commits the result to the store only when it has no ClientErrors.
    /// </summary>
    Task<AggregatedRegistryResponse> GetOrBuildAsync(
        string key,
        TimeSpan? ttl,
        TimeSpan refreshCooldown,
        bool forceRefresh,
        Func<CancellationToken, Task<AggregatedRegistryResponse>> build,
        CancellationToken cancellationToken);
}

// One singleton for every snapshot key (#498): the key is a parameter, the per-key lock lives here.
internal sealed class HttpRegistryCache(IRegistrySnapshotStore store, TimeProvider time) : IHttpRegistryCache, IDisposable
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public void Dispose()
    {
        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }
    }

    /// <summary>
    /// Returns the cached snapshot while it is fresh: younger than
    /// <paramref name="ttl"/> (null = never expires) and not force-refreshed —
    /// except that refresh requests inside the <paramref name="refreshCooldown"/>
    /// window are throttled back to the cache rather than fanning out again.
    /// Otherwise invokes <paramref name="build"/>, stamps
    /// <see cref="AggregatedRegistryResponse.GeneratedAt"/>/<c>IsPartial</c>,
    /// and commits the result to the store only when it has no ClientErrors.
    /// </summary>
    public async Task<AggregatedRegistryResponse> GetOrBuildAsync(
        string key,
        TimeSpan? ttl,
        TimeSpan refreshCooldown,
        bool forceRefresh,
        Func<CancellationToken, Task<AggregatedRegistryResponse>> build,
        CancellationToken cancellationToken)
    {
        var cached = await store.GetAsync(key, cancellationToken);

        if (cached is not null && ServeFromCache(cached, forceRefresh, ttl, refreshCooldown))
        {
            return cached;
        }

        var gate = _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);
        try
        {
            cached = await store.GetAsync(key, cancellationToken);

            if (cached is not null && ServeFromCache(cached, forceRefresh, ttl, refreshCooldown))
            {
                return cached;
            }

            var result = await build(cancellationToken);

            result = result with
            {
                GeneratedAt = time.GetUtcNow(),
                IsPartial = result.ClientErrors.Count > 0,
            };

            if (result.ClientErrors.Count == 0)
            {
                await store.SetAsync(key, result, cancellationToken);
            }

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool ServeFromCache(
        AggregatedRegistryResponse cached,
        bool forceRefresh,
        TimeSpan? ttl,
        TimeSpan refreshCooldown)
    {
        var age = time.GetUtcNow() - cached.GeneratedAt;

        // Throttle: refreshes inside the cooldown window reuse the snapshot
        // instead of triggering another downstream fan-out.
        if (forceRefresh)
        {
            return refreshCooldown > TimeSpan.Zero && age < refreshCooldown;
        }

        return ttl is null || age < ttl.Value;
    }
}

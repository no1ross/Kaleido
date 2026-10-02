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
internal sealed class HttpRegistryCache(
    IRegistrySnapshotStore store,
    string key)
    : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public void Dispose() => _lock.Dispose();

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

        await _lock.WaitAsync(cancellationToken);
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
                GeneratedAt = DateTimeOffset.UtcNow,
                IsPartial = result.ClientErrors.Count > 0
            };

            if (result.ClientErrors.Count == 0)
            {
                await store.SetAsync(key, result, cancellationToken);
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool ServeFromCache(
        AggregatedRegistryResponse cached,
        bool forceRefresh,
        TimeSpan? ttl,
        TimeSpan refreshCooldown)
    {
        var age = DateTimeOffset.UtcNow - cached.GeneratedAt;

        // Throttle: refreshes inside the cooldown window reuse the snapshot
        // instead of triggering another downstream fan-out.
        if (forceRefresh)
        {
            return refreshCooldown > TimeSpan.Zero && age < refreshCooldown;
        }

        return ttl is null || age < ttl.Value;
    }
}

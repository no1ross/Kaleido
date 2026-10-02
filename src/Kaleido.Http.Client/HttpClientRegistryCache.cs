namespace Kaleido.Http.Client;

/// <summary>
/// Thread-safe fetch-once cache for one remote registry payload, backed by
/// <see cref="IRegistrySnapshotStore"/> under the canonical per-service key
/// (<c>kaleido:{routePrefix}</c>). With a distributed store registered, leaf
/// services and router replicas share the same snapshot per service — each
/// service's TTL governs only its own entry. Owns the SemaphoreSlim and
/// double-check locking for <see cref="KaleidoRemoteRegistry"/>. Failed fetches
/// are never stored — the next call retries naturally.
/// </summary>
internal sealed class HttpClientRegistryCache(
    IRegistrySnapshotStore store,
    string key)
    : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTimeOffset _fetchedAtUtc;

    public void Dispose() => _lock.Dispose();

    /// <summary>
    /// Drops the stored snapshot so the next call to <see cref="GetOrFetchAsync"/>
    /// re-fetches from the remote endpoint. With a distributed store this
    /// invalidates across all instances sharing the store.
    /// </summary>
    public void Reset() =>
        store.RemoveAsync(key).AsTask().GetAwaiter().GetResult();

    public async Task<AggregatedRegistryResponse> GetOrFetchAsync(
        TimeSpan? ttl,
        Func<CancellationToken, Task<AggregatedRegistryResponse>> fetch,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.GetAsync(key, cancellationToken);

        if (snapshot is not null && IsFresh(snapshot, ttl))
        {
            return snapshot;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            snapshot = await store.GetAsync(key, cancellationToken);

            if (snapshot is not null && IsFresh(snapshot, ttl))
            {
                return snapshot;
            }

            snapshot = await fetch(cancellationToken);
            _fetchedAtUtc = DateTimeOffset.UtcNow;
            await store.SetAsync(key, snapshot, cancellationToken);
            return snapshot;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsFresh(
        AggregatedRegistryResponse snapshot,
        TimeSpan? ttl)
    {
        if (ttl is null)
        {
            return true;
        }

        // Age basis: prefer the remote's own GeneratedAt (real data age,
        // including the remote's own caching), fall back to when this instance
        // fetched it, else treat as just-seen.
        var basis =
            snapshot.GeneratedAt != default
                ? snapshot.GeneratedAt
                : _fetchedAtUtc != default
                    ? _fetchedAtUtc
                    : DateTimeOffset.UtcNow;

        return DateTimeOffset.UtcNow - basis < ttl.Value;
    }
}

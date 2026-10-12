namespace Kaleido.Http.Client;

/// <summary>
/// Thread-safe cache for one remote registry payload, backed by
/// <see cref="IRegistrySnapshotStore"/> under the canonical per-service key
/// (<c>kaleido:{routePrefix}</c>). With a distributed store registered, leaf
/// services and router replicas share the same snapshot per service — each
/// service's check interval governs only its own entry. Owns the SemaphoreSlim and
/// double-check locking for <see cref="KaleidoRemoteRegistry"/>. Failed fetches
/// are never stored — the next call retries naturally.
/// </summary>
/// <remarks>
/// A snapshot older than the check interval is revalidated, not simply reloaded: the remote is asked
/// whether its revision is still current (ETag), and an unchanged registry costs one small request
/// with an empty 304 answer.
/// </remarks>
internal sealed class HttpClientRegistryCache(
    IRegistrySnapshotStore store,
    string key)
    : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTimeOffset _checkedAtUtc;

    public void Dispose() => _lock.Dispose();

    /// <summary>
    /// Drops the stored snapshot so the next call re-fetches from the remote endpoint. With a
    /// distributed store this invalidates across all instances sharing the store.
    /// </summary>
    public void Reset() =>
        store.RemoveAsync(key).AsTask().GetAwaiter().GetResult();

    /// <summary>The snapshot while fresh; otherwise <paramref name="fetch"/> loads it again.</summary>
    public Task<AggregatedRegistryResponse> GetOrFetchAsync(
        TimeSpan? checkInterval,
        Func<CancellationToken, Task<AggregatedRegistryResponse>> fetch,
        CancellationToken cancellationToken) =>
        GetOrRevalidateAsync(
            checkInterval,
            async (_, ct) => await fetch(ct),
            cancellationToken);

    /// <summary>
    /// The snapshot while fresh (checked within <paramref name="checkInterval"/>; null = never
    /// checked again); otherwise <paramref name="revalidate"/> is called with the snapshot's revision
    /// (null when there is no snapshot) and returns the new registry, or null when the remote reports
    /// the revision unchanged - the snapshot is then kept and counts as checked.
    /// </summary>
    public async Task<AggregatedRegistryResponse> GetOrRevalidateAsync(
        TimeSpan? checkInterval,
        Func<string?, CancellationToken, Task<AggregatedRegistryResponse?>> revalidate,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.GetAsync(key, cancellationToken);

        if (snapshot is not null && IsFresh(snapshot, checkInterval))
        {
            return snapshot;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            snapshot = await store.GetAsync(key, cancellationToken);

            if (snapshot is not null && IsFresh(snapshot, checkInterval))
            {
                return snapshot;
            }

            var current = await revalidate(snapshot?.Revision, cancellationToken);
            _checkedAtUtc = DateTimeOffset.UtcNow;

            if (current is null)
            {
                return snapshot
                    ?? throw new KaleidoHttpClientException(
                        HttpClientErrorCodes.EmptyResponse,
                        $"Registry '{key}' was reported unchanged, but there is no snapshot to keep.",
                        System.Net.HttpStatusCode.NotModified);
            }

            await store.SetAsync(key, current, cancellationToken);
            return current;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsFresh(
        AggregatedRegistryResponse snapshot,
        TimeSpan? checkInterval)
    {
        if (checkInterval is null)
        {
            return true;
        }

        // Age basis: the later of the remote's own GeneratedAt (real data age, including the
        // remote's own caching) and the last time this instance checked it; else treat as just-seen.
        var basis =
            snapshot.GeneratedAt > _checkedAtUtc
                ? snapshot.GeneratedAt
                : _checkedAtUtc;

        if (basis == default)
        {
            return true;
        }

        return DateTimeOffset.UtcNow - basis < checkInterval.Value;
    }
}

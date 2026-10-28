using System.Collections.Concurrent;

namespace Kaleido.Http.Client;

/// <summary>
/// The remote registry payloads this host fetched, backed by <see cref="IRegistrySnapshotStore"/>
/// under each client's own key (<c>kaleido-client:{ServiceName}:{clientName}</c>, never the remote
/// service's <c>kaleido:{ServiceName}</c>). With a distributed store registered, replicas of the
/// consuming host share the same entry per client. Failed fetches are never stored - the next call
/// retries naturally.
/// </summary>
/// <remarks>
/// A snapshot older than the check interval is revalidated, not simply reloaded: the remote is asked
/// whether its revision is still current (ETag), and an unchanged registry costs one small request
/// with an empty 304 answer. One singleton for all clients (#498): the key is a parameter, and the
/// per-key lock and last check live here.
/// </remarks>
internal interface IHttpClientRegistryCache
{
    /// <summary>
    /// Drops the stored snapshot of <paramref name="key"/> so the next call re-fetches it. With a
    /// distributed store this invalidates across all instances sharing the store.
    /// </summary>
    ValueTask ResetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// The snapshot of <paramref name="key"/> while fresh (checked within
    /// <paramref name="checkInterval"/>; null = never checked again); otherwise
    /// <paramref name="revalidate"/> is called with the snapshot's revision (null when there is no
    /// snapshot) and returns the new registry, or null when the remote reports the revision unchanged -
    /// the snapshot is then kept and counts as checked.
    /// </summary>
    Task<AggregatedRegistryResponse> GetOrRevalidateAsync(
        string key,
        TimeSpan? checkInterval,
        Func<string?, CancellationToken, Task<AggregatedRegistryResponse?>> revalidate,
        CancellationToken cancellationToken);
}

internal sealed class HttpClientRegistryCache(IRegistrySnapshotStore store) : IHttpClientRegistryCache, IDisposable
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public void Dispose()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Lock.Dispose();
        }
    }

    public ValueTask ResetAsync(string key, CancellationToken cancellationToken) =>
        store.RemoveAsync(key, cancellationToken);

    public async Task<AggregatedRegistryResponse> GetOrRevalidateAsync(
        string key,
        TimeSpan? checkInterval,
        Func<string?, CancellationToken, Task<AggregatedRegistryResponse?>> revalidate,
        CancellationToken cancellationToken)
    {
        var entry = _entries.GetOrAdd(key, static _ => new Entry());
        var snapshot = await store.GetAsync(key, cancellationToken);

        if (snapshot is not null && IsFresh(entry, snapshot, checkInterval))
        {
            return snapshot;
        }

        await entry.Lock.WaitAsync(cancellationToken);
        try
        {
            snapshot = await store.GetAsync(key, cancellationToken);

            if (snapshot is not null && IsFresh(entry, snapshot, checkInterval))
            {
                return snapshot;
            }

            var current = await revalidate(snapshot?.Revision, cancellationToken);
            entry.CheckedAtUtc = DateTimeOffset.UtcNow;

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
            entry.Lock.Release();
        }
    }

    private static bool IsFresh(Entry entry, AggregatedRegistryResponse snapshot, TimeSpan? checkInterval)
    {
        if (checkInterval is null)
        {
            return true;
        }

        // Age basis: the later of the remote's own GeneratedAt (real data age, including the
        // remote's own caching) and the last time this instance checked it; else treat as just-seen.
        var basis =
            snapshot.GeneratedAt > entry.CheckedAtUtc
                ? snapshot.GeneratedAt
                : entry.CheckedAtUtc;

        if (basis == default)
        {
            return true;
        }

        return DateTimeOffset.UtcNow - basis < checkInterval.Value;
    }

    // One key's lock (double-checked fetch) and the last time this instance checked it.
    private sealed class Entry
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);

        public DateTimeOffset CheckedAtUtc { get; set; }
    }
}

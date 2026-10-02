namespace Kaleido.Http.Registry;

/// <summary>
/// Storage seam for registry snapshots — the same pattern as
/// <c>IProcessContextStore</c>. The framework ships an in-memory default;
/// aggregating hosts (routers/gateways) can register a distributed
/// implementation (e.g. Redis over <c>IDistributedCache</c>) so replicas share
/// one merged snapshot and one fan-out per refresh window.
/// </summary>
/// <remarks>
/// The store is a dumb key/value holder: TTL, refresh cooldowns,
/// clean-only-commit, and request coalescing are enforced by the callers
/// (<c>HttpRegistryCache</c> server-side, <c>KaleidoRemoteRegistry</c>
/// client-side), not by the store. Store expiry features are intentionally not
/// used so in-memory and distributed stores behave identically.
/// Only clean (non-partial) snapshots are ever written, and they always carry
/// a populated <see cref="AggregatedRegistryResponse.GeneratedAt"/>.
/// </remarks>
public interface IRegistrySnapshotStore
{
    /// <summary>Returns the snapshot for <paramref name="key"/>, or <c>null</c> when absent.</summary>
    ValueTask<AggregatedRegistryResponse?> GetAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>Stores (or replaces) the snapshot for <paramref name="key"/>.</summary>
    ValueTask SetAsync(
        string key,
        AggregatedRegistryResponse snapshot,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the snapshot for <paramref name="key"/> if present.</summary>
    ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IRegistrySnapshotStore"/> — process-local
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>.
/// Appropriate for leaf services; aggregators that run multiple replicas should
/// register a distributed implementation instead.
/// </summary>
public sealed class InMemoryRegistrySnapshotStore
    : IRegistrySnapshotStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, AggregatedRegistryResponse> _snapshots =
        new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<AggregatedRegistryResponse?> GetAsync(
        string key,
        CancellationToken cancellationToken = default) =>
        new(_snapshots.TryGetValue(key, out var snapshot) ? snapshot : null);

    public ValueTask SetAsync(
        string key,
        AggregatedRegistryResponse snapshot,
        CancellationToken cancellationToken = default)
    {
        _snapshots[key] = snapshot;
        return default;
    }

    public ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        _snapshots.TryRemove(key, out _);
        return default;
    }
}

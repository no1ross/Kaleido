namespace Kaleido.Http.Registry;

/// <summary>
/// Storage seam for registry snapshots — the same pattern as
/// <c>IKaleidoContextStore</c>. The framework ships an in-memory default;
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
/// <para>
/// Every call is awaited with the caller's cancellation token; nothing blocks on it. An exception
/// from the store is the caller's failure (the registry read fails and nothing is stored), so a
/// store that can be unavailable handles that itself, for example by falling back to memory.
/// Registered with <c>TryAdd</c> by both <c>AddHttp()</c> and <c>AddHttpClients()</c>: register your
/// implementation before them to replace the in-memory default (ADR 0017).
/// </para>
/// </remarks>
public interface IRegistrySnapshotStore
{
    /// <summary>Returns the snapshot for <paramref name="key"/>, or <see langword="null"/> when absent.</summary>
    ValueTask<AggregatedRegistryResponse?> GetAsync(
        string key,
        CancellationToken cancellationToken);

    /// <summary>Stores (or replaces) the snapshot for <paramref name="key"/>.</summary>
    ValueTask SetAsync(
        string key,
        AggregatedRegistryResponse snapshot,
        CancellationToken cancellationToken);

    /// <summary>Removes the snapshot for <paramref name="key"/> if present.</summary>
    ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken);
}

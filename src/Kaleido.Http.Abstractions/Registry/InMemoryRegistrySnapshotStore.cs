namespace Kaleido.Http.Registry;

/// <summary>
/// Default <see cref="IRegistrySnapshotStore"/> — process-local
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>.
/// Appropriate for leaf services; aggregators that run multiple replicas should
/// register a distributed implementation instead.
/// </summary>
internal sealed class InMemoryRegistrySnapshotStore
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

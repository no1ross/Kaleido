using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Registry;

/// <summary>
/// <see cref="IRegistrySnapshotStore"/> on the host's <see cref="IDistributedCache"/>, registered by
/// <see cref="KaleidoRegistrySnapshotServiceCollectionExtensions.AddDistributedRegistrySnapshots"/>:
/// replicas share one snapshot per key (#275).
/// </summary>
/// <remarks>
/// <para>
/// A snapshot is stored as its JSON (<see cref="KaleidoJsonOptions"/>, the wire format) without a
/// cache expiry: freshness is decided by the callers from <see cref="AggregatedRegistryResponse.GeneratedAt"/>,
/// as with the in-memory store. An entry that cannot be read counts as absent, so the caller rebuilds it.
/// </para>
/// <para>
/// The cache never fails a registry read. Every snapshot is also kept in memory. When the cache throws
/// (anything but the caller's own cancellation), the store logs a warning once, serves and keeps
/// snapshots in memory, and leaves the cache alone for <see cref="RetryAfter"/>, so requests do not
/// each wait on a cache timeout during an outage.
/// </para>
/// </remarks>
internal sealed partial class DistributedRegistrySnapshotStore(
    IDistributedCache cache,
    ILogger<DistributedRegistrySnapshotStore> logger,
    TimeProvider timeProvider)
    : IRegistrySnapshotStore
{
    /// <summary>How long the cache is left alone after it failed.</summary>
    internal static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(30);

    private static readonly DistributedCacheEntryOptions NoExpiry = new();

    private readonly InMemoryRegistrySnapshotStore _memory = new();
    private long _unavailableUntilUtcTicks;

    public async ValueTask<AggregatedRegistryResponse?> GetAsync(
        string key,
        CancellationToken cancellationToken)
    {
        if (IsAvailable())
        {
            try
            {
                var snapshot = Read(key, await cache.GetAsync(key, cancellationToken));
                await (snapshot is null
                    ? _memory.RemoveAsync(key, cancellationToken)
                    : _memory.SetAsync(key, snapshot, cancellationToken));
                return snapshot;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                MarkUnavailable(exception);
            }
        }

        return await _memory.GetAsync(key, cancellationToken);
    }

    public async ValueTask SetAsync(
        string key,
        AggregatedRegistryResponse snapshot,
        CancellationToken cancellationToken)
    {
        await _memory.SetAsync(key, snapshot, cancellationToken);

        if (IsAvailable())
        {
            try
            {
                await cache.SetAsync(
                    key,
                    JsonSerializer.SerializeToUtf8Bytes(snapshot, KaleidoJsonOptions.Options),
                    NoExpiry,
                    cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                MarkUnavailable(exception);
            }
        }
    }

    public async ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken)
    {
        await _memory.RemoveAsync(key, cancellationToken);

        if (IsAvailable())
        {
            try
            {
                await cache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                MarkUnavailable(exception);
            }
        }
    }

    private AggregatedRegistryResponse? Read(string key, byte[]? entry)
    {
        if (entry is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AggregatedRegistryResponse>(entry, KaleidoJsonOptions.Options);
        }
        catch (JsonException exception)
        {
            LogUnreadableEntry(logger, key, exception);
            return null;
        }
    }

    private bool IsAvailable() =>
        timeProvider.GetUtcNow().UtcTicks >= Interlocked.Read(ref _unavailableUntilUtcTicks);

    private void MarkUnavailable(Exception exception)
    {
        var now = timeProvider.GetUtcNow();
        var previous = Interlocked.Exchange(ref _unavailableUntilUtcTicks, (now + RetryAfter).UtcTicks);

        // Once per outage: concurrent failures inside the same window stay quiet.
        if (previous <= now.UtcTicks)
        {
            LogCacheUnavailable(logger, RetryAfter.TotalSeconds, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The distributed cache for registry snapshots is unavailable; snapshots are served from memory and the cache is retried in {RetryAfterSeconds} s.")]
    private static partial void LogCacheUnavailable(ILogger logger, double retryAfterSeconds, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "The registry snapshot '{Key}' in the distributed cache could not be read; it counts as absent and is rebuilt.")]
    private static partial void LogUnreadableEntry(ILogger logger, string key, Exception exception);
}

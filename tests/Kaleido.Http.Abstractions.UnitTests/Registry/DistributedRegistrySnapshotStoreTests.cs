using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class DistributedRegistrySnapshotStoreTests
    : Kaleido.UnitTests.SutFixture
{
    private const string Key = "kaleido:router";

    private static readonly AggregatedRegistryResponse Snapshot = new()
    {
        GeneratedAt = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero),
        Revision = "abc123",
    };

    private static FakeTimeProvider NewClock() => new(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static DistributedRegistrySnapshotStore CreateSut(
        IDistributedCache cache,
        FakeTimeProvider? time = null,
        ILogger<DistributedRegistrySnapshotStore>? logger = null) =>
        new(cache, MemoryStore(), logger ?? NullLogger<DistributedRegistrySnapshotStore>.Instance, time ?? NewClock());

    // The snapshots this replica keeps in memory (the in-memory store's own rules are tested in its fixture).
    private static IRegistrySnapshotStore MemoryStore()
    {
        var data = new ConcurrentDictionary<string, AggregatedRegistryResponse>(StringComparer.Ordinal);
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string k, CancellationToken _) => new ValueTask<AggregatedRegistryResponse?>(data.TryGetValue(k, out var v) ? v : null));
        store.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AggregatedRegistryResponse>(), It.IsAny<CancellationToken>()))
            .Returns((string k, AggregatedRegistryResponse v, CancellationToken _) =>
            {
                data[k] = v;
                return ValueTask.CompletedTask;
            });
        store.Setup(s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string k, CancellationToken _) =>
            {
                data.TryRemove(k, out var _);
                return ValueTask.CompletedTask;
            });
        return store.Object;
    }

    [Fact]
    public async Task SetAsync_ThenGetOnAnotherReplica_ReturnsTheSameSnapshot()
    {
        var cache = new DictionaryCache();
        var replicaA = CreateSut(cache);
        var replicaB = CreateSut(cache);

        await replicaA.SetAsync(Key, Snapshot, Cancel);
        var result = await replicaB.GetAsync(Key, Cancel);

        Assert.NotNull(result);
        Assert.Equal(Snapshot.GeneratedAt, result.GeneratedAt);
        Assert.Equal(Snapshot.Revision, result.Revision);
    }

    [Fact]
    public async Task RemoveAsync_OnOneReplica_IsSeenByTheOthers()
    {
        var cache = new DictionaryCache();
        var replicaA = CreateSut(cache);
        var replicaB = CreateSut(cache);
        await replicaA.SetAsync(Key, Snapshot, Cancel);
        Assert.NotNull(await replicaA.GetAsync(Key, Cancel));

        await replicaB.RemoveAsync(Key, Cancel);

        Assert.Null(await replicaA.GetAsync(Key, Cancel));
    }

    [Fact]
    public async Task GetAsync_WhenTheEntryCannotBeRead_ReturnsNull()
    {
        var cache = new DictionaryCache();
        cache.Entries[Key] = Encoding.UTF8.GetBytes("not json");

        Assert.Null(await CreateSut(cache).GetAsync(Key, Cancel));
    }

    [Fact]
    public async Task GetAsync_WhenTheCacheFails_ServesTheReplicasOwnSnapshot()
    {
        var cache = new DictionaryCache();
        var sut = CreateSut(cache);
        await sut.SetAsync(Key, Snapshot, Cancel);

        cache.Fail = true;
        var result = await sut.GetAsync(Key, Cancel);

        Assert.NotNull(result);
        Assert.Equal(Snapshot.Revision, result.Revision);
    }

    [Fact]
    public async Task SetAsync_WhenTheCacheFails_KeepsTheSnapshotInMemory()
    {
        var cache = new DictionaryCache { Fail = true };
        var sut = CreateSut(cache);

        await sut.SetAsync(Key, Snapshot, Cancel);

        Assert.Equal(Snapshot.Revision, (await sut.GetAsync(Key, Cancel))?.Revision);
    }

    [Fact]
    public async Task RemoveAsync_WhenTheCacheFails_RemovesTheMemoryCopy()
    {
        var cache = new DictionaryCache();
        var sut = CreateSut(cache);
        await sut.SetAsync(Key, Snapshot, Cancel);

        cache.Fail = true;
        await sut.RemoveAsync(Key, Cancel);

        Assert.Null(await sut.GetAsync(Key, Cancel));
    }

    [Fact]
    public async Task AfterAFailure_TheCacheIsLeftAloneUntilRetryAfter()
    {
        var cache = new DictionaryCache { Fail = true };
        var time = NewClock();
        var sut = CreateSut(cache, time);

        await sut.GetAsync(Key, Cancel);
        await sut.GetAsync(Key, Cancel);
        await sut.SetAsync(Key, Snapshot, Cancel);
        Assert.Equal(1, cache.Calls);

        time.Advance(DistributedRegistrySnapshotStore.RetryAfter);
        cache.Fail = false;
        await sut.GetAsync(Key, Cancel);

        Assert.Equal(2, cache.Calls);
    }

    [Fact]
    public async Task AFailure_IsLoggedOncePerOutage()
    {
        var cache = new DictionaryCache { Fail = true };
        var time = NewClock();
        var logger = new Mock<ILogger<DistributedRegistrySnapshotStore>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var sut = CreateSut(cache, time, logger.Object);

        await sut.GetAsync(Key, Cancel);
        await sut.GetAsync(Key, Cancel);
        time.Advance(DistributedRegistrySnapshotStore.RetryAfter);
        await sut.GetAsync(Key, Cancel);

        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_WhenTheCallerCancels_Throws_AndKeepsUsingTheCache()
    {
        var cache = new DictionaryCache();
        var sut = CreateSut(cache);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.GetAsync(Key, canceled.Token).AsTask());
        await sut.GetAsync(Key, Cancel);

        Assert.Equal(2, cache.Calls);
    }

    private sealed class DictionaryCache : IDistributedCache
    {
        public ConcurrentDictionary<string, byte[]> Entries { get; } = new();

        public bool Fail { get; set; }

        public int Calls { get; private set; }

        public byte[]? Get(string key) => throw new NotSupportedException();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            Enter(token);
            return Task.FromResult(Entries.TryGetValue(key, out var value) ? value : null);
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Enter(token);
            Entries[key] = value;
            return Task.CompletedTask;
        }

        public void Refresh(string key) => throw new NotSupportedException();

        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();

        public void Remove(string key) => throw new NotSupportedException();

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Enter(token);
            Entries.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        private void Enter(CancellationToken token)
        {
            Calls++;
            token.ThrowIfCancellationRequested();

            if (Fail)
            {
                throw new InvalidOperationException("The cache is unreachable.");
            }
        }
    }
}

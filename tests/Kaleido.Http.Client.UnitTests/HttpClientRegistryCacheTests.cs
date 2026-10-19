using Kaleido.Http.Client;
using Kaleido.Http.Registry;

namespace Kaleido.Http.Client.UnitTests;

public sealed class HttpClientRegistryCacheTests
    : Kaleido.UnitTests.SutFixture
{
    private const string Key = "kaleido:remote";

    private static HttpClientRegistryCache CreateSut(
        IRegistrySnapshotStore? store = null) =>
        new(store ?? CreateStore(), Key);

    private static IRegistrySnapshotStore CreateStore(
        System.Collections.Concurrent.ConcurrentDictionary<string, AggregatedRegistryResponse>? backing = null)
    {
        var data = backing ?? new System.Collections.Concurrent.ConcurrentDictionary<string, AggregatedRegistryResponse>(StringComparer.OrdinalIgnoreCase);
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string k, CancellationToken _) =>
            {
                data.TryGetValue(k, out var snapshot);
                return new ValueTask<AggregatedRegistryResponse?>(snapshot);
            });
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

    private static readonly AggregatedRegistryResponse Fresh = new()
    {
        GeneratedAt = DateTimeOffset.UtcNow
    };

    private static readonly AggregatedRegistryResponse Stale = new()
    {
        GeneratedAt = DateTimeOffset.UtcNow.AddHours(-1)
    };

    [Fact]
    public async Task GetOrFetchAsync_WhenNothingCached_InvokesFetch()
    {
        using var sut = CreateSut();
        var fetchCalled = false;

        var result = await sut.GetOrFetchAsync(
            null,
            _ =>
            {
                fetchCalled = true;
                return Task.FromResult(Fresh);
            },
            CancellationToken.None);

        Assert.True(fetchCalled);
        Assert.Same(Fresh, result);
    }

    [Fact]
    public async Task GetOrFetchAsync_WhenAlreadyCached_DoesNotInvokeFetchAgain()
    {
        using var sut = CreateSut();

        await sut.GetOrFetchAsync(null, _ => Task.FromResult(Fresh), CancellationToken.None);

        var fetchCount = 0;
        var result = await sut.GetOrFetchAsync(
            null,
            _ =>
            {
                fetchCount++;
                return Task.FromResult(Stale);
            },
            CancellationToken.None);

        Assert.Equal(0, fetchCount);
        Assert.Same(Fresh, result);
    }

    [Fact]
    public async Task GetOrFetchAsync_WhenSnapshotOlderThanTtl_Refetches()
    {
        var store = CreateStore();
        await store.SetAsync(Key, Stale);

        using var sut = CreateSut(store);
        var fetchCount = 0;

        var result = await sut.GetOrFetchAsync(
            TimeSpan.FromMinutes(5),
            _ =>
            {
                fetchCount++;
                return Task.FromResult(Fresh);
            },
            CancellationToken.None);

        Assert.Equal(1, fetchCount);
        Assert.Same(Fresh, result);
    }

    [Fact]
    public async Task GetOrFetchAsync_WhenSnapshotWithinTtl_ServesCached()
    {
        var store = CreateStore();
        await store.SetAsync(Key, Fresh);

        using var sut = CreateSut(store);
        var fetchCount = 0;

        var result = await sut.GetOrFetchAsync(
            TimeSpan.FromMinutes(5),
            _ =>
            {
                fetchCount++;
                return Task.FromResult(Stale);
            },
            CancellationToken.None);

        Assert.Equal(0, fetchCount);
        Assert.Same(Fresh, result);
    }

    [Fact]
    public async Task GetOrFetchAsync_SharesSnapshotAcrossInstances()
    {
        var store = CreateStore();

        using var first = CreateSut(store);
        await first.GetOrFetchAsync(null, _ => Task.FromResult(Fresh), CancellationToken.None);

        using var second = CreateSut(store);
        var fetchCount = 0;
        var result = await second.GetOrFetchAsync(
            null,
            _ =>
            {
                fetchCount++;
                return Task.FromResult(Stale);
            },
            CancellationToken.None);

        Assert.Equal(0, fetchCount);
        Assert.Same(Fresh, result);
    }

    [Fact]
    public async Task ResetAsync_ClearsCache_SoNextFetchIsInvoked()
    {
        using var sut = CreateSut();

        await sut.GetOrFetchAsync(null, _ => Task.FromResult(Fresh), CancellationToken.None);

        await sut.ResetAsync(CancellationToken.None);

        var fetchCalled = false;
        var result = await sut.GetOrFetchAsync(
            null,
            _ =>
            {
                fetchCalled = true;
                return Task.FromResult(Stale);
            },
            CancellationToken.None);

        Assert.True(fetchCalled);
        Assert.Same(Stale, result);
    }

    [Fact]
    public async Task ResetAsync_AwaitsAnAsynchronousStore_WithoutBlocking_AndPassesTheToken()
    {
        var removal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken passed = default;
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.RemoveAsync(Key, It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken token) =>
            {
                passed = token;
                return new ValueTask(removal.Task);
            });
        using var cancellation = new CancellationTokenSource();
        using var sut = CreateSut(store.Object);

        var reset = sut.ResetAsync(cancellation.Token).AsTask();

        Assert.False(reset.IsCompleted);
        removal.SetResult();
        await reset;
        Assert.Equal(cancellation.Token, passed);
    }

    private static readonly AggregatedRegistryResponse StaleWithRevision = new()
    {
        GeneratedAt = DateTimeOffset.UtcNow.AddHours(-1),
        Revision = "abc123",
    };

    [Fact]
    public async Task GetOrRevalidateAsync_WhenTheSnapshotIsDue_ChecksWithItsRevision()
    {
        var store = CreateStore();
        await store.SetAsync(Key, StaleWithRevision);
        using var sut = CreateSut(store);
        string? sentRevision = null;

        await sut.GetOrRevalidateAsync(
            TimeSpan.FromSeconds(30),
            (revision, _) =>
            {
                sentRevision = revision;
                return Task.FromResult<AggregatedRegistryResponse?>(null);
            },
            CancellationToken.None);

        Assert.Equal("abc123", sentRevision);
    }

    [Fact]
    public async Task GetOrRevalidateAsync_WhenUnchanged_KeepsTheSnapshotAndCountsItAsChecked()
    {
        var store = CreateStore();
        await store.SetAsync(Key, StaleWithRevision);
        using var sut = CreateSut(store);
        var checks = 0;

        Task<AggregatedRegistryResponse?> Unchanged(string? revision, CancellationToken _)
        {
            checks++;
            return Task.FromResult<AggregatedRegistryResponse?>(null);
        }

        var first = await sut.GetOrRevalidateAsync(TimeSpan.FromSeconds(30), Unchanged, CancellationToken.None);
        var second = await sut.GetOrRevalidateAsync(TimeSpan.FromSeconds(30), Unchanged, CancellationToken.None);

        Assert.Same(StaleWithRevision, first);
        Assert.Same(StaleWithRevision, second);
        Assert.Equal(1, checks);
    }

    [Fact]
    public async Task GetOrRevalidateAsync_WhenChanged_StoresAndReturnsTheNewRegistry()
    {
        var store = CreateStore();
        await store.SetAsync(Key, StaleWithRevision);
        using var sut = CreateSut(store);

        var result = await sut.GetOrRevalidateAsync(
            TimeSpan.FromSeconds(30),
            (_, _) => Task.FromResult<AggregatedRegistryResponse?>(Fresh),
            CancellationToken.None);

        Assert.Same(Fresh, result);
        Assert.Same(Fresh, await store.GetAsync(Key));
    }

    [Fact]
    public async Task GetOrRevalidateAsync_ASnapshotWithoutAnAge_IsServedWithoutChecking()
    {
        var store = CreateStore();
        var undated = new AggregatedRegistryResponse { Revision = "r1" };
        await store.SetAsync(Key, undated);
        using var sut = CreateSut(store);
        var checks = 0;

        var result = await sut.GetOrRevalidateAsync(
            TimeSpan.FromSeconds(30),
            (_, _) =>
            {
                checks++;
                return Task.FromResult<AggregatedRegistryResponse?>(Fresh);
            },
            CancellationToken.None);

        Assert.Same(undated, result);
        Assert.Equal(0, checks);
    }

    [Fact]
    public async Task GetOrFetchAsync_ACallerWaitingForAnotherFetch_ServesItsResultWithoutFetchingAgain()
    {
        using var sut = CreateSut();
        var release = new TaskCompletionSource<AggregatedRegistryResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fetches = 0;

        Task<AggregatedRegistryResponse> Fetch(CancellationToken _)
        {
            fetches++;
            return release.Task;
        }

        var first = sut.GetOrFetchAsync(null, Fetch, CancellationToken.None);
        var second = sut.GetOrFetchAsync(null, Fetch, CancellationToken.None);
        release.SetResult(Fresh);

        Assert.Same(Fresh, await first);
        Assert.Same(Fresh, await second);
        Assert.Equal(1, fetches);
    }

    [Fact]
    public async Task GetOrRevalidateAsync_WhenReportedUnchangedWithoutASnapshot_Throws()
    {
        using var sut = CreateSut();

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(() =>
            sut.GetOrRevalidateAsync(
                TimeSpan.FromSeconds(30),
                (_, _) => Task.FromResult<AggregatedRegistryResponse?>(null),
                CancellationToken.None));

        Assert.Equal(HttpClientErrorCodes.EmptyResponse, exception.Code);
    }

    [Fact]
    public async Task GetOrFetchAsync_PassesTheCallersTokenToTheStoreAndTheFetch()
    {
        using var cts = new CancellationTokenSource();
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(Key, cts.Token)).ReturnsAsync((AggregatedRegistryResponse?)null);
        using var sut = CreateSut(store.Object);
        var fetchToken = CancellationToken.None;

        await sut.GetOrFetchAsync(null, ct => { fetchToken = ct; return Task.FromResult(Fresh); }, cts.Token);

        Assert.Equal(cts.Token, fetchToken);
        store.Verify(s => s.SetAsync(Key, Fresh, cts.Token), Times.Once);
    }

    // A store failure is the caller's failure; a store that may be unavailable handles that itself
    // (a distributed store falls back, #275).
    [Fact]
    public async Task GetOrFetchAsync_WhenTheStoreFails_TheFailurePropagates_AndNothingIsFetched()
    {
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(Key, It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("store down"));
        using var sut = CreateSut(store.Object);
        var fetched = false;

        await Assert.ThrowsAsync<TimeoutException>(() => sut.GetOrFetchAsync(null, _ => { fetched = true; return Task.FromResult(Fresh); }, TestContext.Current.CancellationToken));

        Assert.False(fetched);
    }

    [Fact]
    public async Task GetOrFetchAsync_WhenTheFetchFails_NothingIsStored_AndTheNextCallFetchesAgain()
    {
        var backing = new System.Collections.Concurrent.ConcurrentDictionary<string, AggregatedRegistryResponse>(StringComparer.OrdinalIgnoreCase);
        using var sut = CreateSut(CreateStore(backing));

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetOrFetchAsync(null, _ => throw new HttpRequestException("down"), TestContext.Current.CancellationToken));
        Assert.Empty(backing);

        var result = await sut.GetOrFetchAsync(null, _ => Task.FromResult(Fresh), TestContext.Current.CancellationToken);
        Assert.Same(Fresh, result);
    }
}

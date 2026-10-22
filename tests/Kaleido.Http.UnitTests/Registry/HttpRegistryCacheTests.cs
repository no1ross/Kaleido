namespace Kaleido.Http.Registry.UnitTests;

public sealed class HttpRegistryCacheTests
    : Kaleido.UnitTests.SutFixture
{
    private const string Key = "kaleido:test";

    private static HttpRegistryCache CreateSut(
        IRegistrySnapshotStore? store = null) =>
        new(store ?? CreateStore(), Key);

    private static IRegistrySnapshotStore CreateStore()
    {
        var data = new System.Collections.Concurrent.ConcurrentDictionary<string, AggregatedRegistryResponse>(StringComparer.OrdinalIgnoreCase);
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

    private static Task<AggregatedRegistryResponse> Clean() =>
        Task.FromResult(new AggregatedRegistryResponse());

    private static Task<AggregatedRegistryResponse> Partial() =>
        Task.FromResult(new AggregatedRegistryResponse
        {
            ClientErrors =
            [
                new RegistryClientError
                {
                    ClientName = "down",
                    ClientType = "Registry",
                    Reason = "unreachable"
                }
            ]
        });

    [Fact]
    public async Task GetOrBuildAsync_WhenNoCachedResult_InvokesBuild()
    {
        using var sut = CreateSut();
        var buildCalled = false;

        var result = await sut.GetOrBuildAsync(
            ttl: null,
            refreshCooldown: TimeSpan.FromSeconds(30),
            forceRefresh: false,
            build: _ =>
            {
                buildCalled = true;
                return Clean();
            },
            cancellationToken: default);

        Assert.True(buildCalled);
        Assert.NotEqual(default, result.GeneratedAt);
        Assert.False(result.IsPartial);
    }

    [Fact]
    public async Task GetOrBuildAsync_WhenCachedAndNoForceRefresh_ReturnsCachedResult()
    {
        using var sut = CreateSut();

        var first = await sut.GetOrBuildAsync(null, TimeSpan.FromSeconds(30), false, _ => Clean(), default);

        var buildCount = 0;
        var result = await sut.GetOrBuildAsync(
            ttl: null,
            refreshCooldown: TimeSpan.FromSeconds(30),
            forceRefresh: false,
            build: _ =>
            {
                buildCount++;
                return Clean();
            },
            cancellationToken: default);

        Assert.Equal(0, buildCount);
        Assert.Same(first, result);
    }

    [Fact]
    public async Task GetOrBuildAsync_PartialResult_IsServedButNotCached()
    {
        using var sut = CreateSut();

        var partial = await sut.GetOrBuildAsync(null, TimeSpan.Zero, false, _ => Partial(), default);
        Assert.True(partial.IsPartial);

        var buildCount = 0;
        var result = await sut.GetOrBuildAsync(
            ttl: null,
            refreshCooldown: TimeSpan.Zero,
            forceRefresh: false,
            build: _ =>
            {
                buildCount++;
                return Clean();
            },
            cancellationToken: default);

        Assert.Equal(1, buildCount);
        Assert.False(result.IsPartial);
    }

    [Fact]
    public async Task GetOrBuildAsync_WhenSnapshotOlderThanTtl_Rebuilds()
    {
        var store = CreateStore();
        await store.SetAsync(Key, new AggregatedRegistryResponse
        {
            GeneratedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });

        using var sut = CreateSut(store);
        var buildCount = 0;

        await sut.GetOrBuildAsync(
            ttl: TimeSpan.FromMinutes(5),
            refreshCooldown: TimeSpan.FromSeconds(30),
            forceRefresh: false,
            build: _ =>
            {
                buildCount++;
                return Clean();
            },
            cancellationToken: default);

        Assert.Equal(1, buildCount);
    }

    [Fact]
    public async Task GetOrBuildAsync_WhenSnapshotWithinTtl_ServesCached()
    {
        var store = CreateStore();
        await store.SetAsync(Key, new AggregatedRegistryResponse
        {
            GeneratedAt = DateTimeOffset.UtcNow
        });

        using var sut = CreateSut(store);
        var buildCount = 0;

        await sut.GetOrBuildAsync(
            ttl: TimeSpan.FromMinutes(5),
            refreshCooldown: TimeSpan.FromSeconds(30),
            forceRefresh: false,
            build: _ =>
            {
                buildCount++;
                return Clean();
            },
            cancellationToken: default);

        Assert.Equal(0, buildCount);
    }

    [Fact]
    public async Task GetOrBuildAsync_RefreshInsideCooldown_ServesCache()
    {
        using var sut = CreateSut();

        await sut.GetOrBuildAsync(null, TimeSpan.FromSeconds(30), false, _ => Clean(), default);

        var buildCount = 0;
        await sut.GetOrBuildAsync(
            ttl: null,
            refreshCooldown: TimeSpan.FromSeconds(30),
            forceRefresh: true,
            build: _ =>
            {
                buildCount++;
                return Clean();
            },
            cancellationToken: default);

        Assert.Equal(0, buildCount);
    }

    [Fact]
    public async Task GetOrBuildAsync_WhenAnotherCallerBuiltWhileWaiting_ServesThatSnapshot()
    {
        var builtMeanwhile = new AggregatedRegistryResponse { GeneratedAt = DateTimeOffset.UtcNow, Revision = "meanwhile" };
        var store = new Mock<IRegistrySnapshotStore>();
        store.SetupSequence(s => s.GetAsync(Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AggregatedRegistryResponse?)null)
            .ReturnsAsync(builtMeanwhile);
        using var sut = CreateSut(store.Object);
        var buildCount = 0;

        var result = await sut.GetOrBuildAsync(
            ttl: null,
            refreshCooldown: TimeSpan.FromSeconds(30),
            forceRefresh: false,
            build: _ =>
            {
                buildCount++;
                return Clean();
            },
            cancellationToken: default);

        Assert.Same(builtMeanwhile, result);
        Assert.Equal(0, buildCount);
        store.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AggregatedRegistryResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrBuildAsync_RefreshWithoutACooldown_AlwaysRebuilds()
    {
        using var sut = CreateSut();
        await sut.GetOrBuildAsync(null, TimeSpan.Zero, false, _ => Clean(), default);
        var buildCount = 0;

        await sut.GetOrBuildAsync(
            ttl: null,
            refreshCooldown: TimeSpan.Zero,
            forceRefresh: true,
            build: _ =>
            {
                buildCount++;
                return Clean();
            },
            cancellationToken: default);

        Assert.Equal(1, buildCount);
    }

    [Fact]
    public async Task GetOrBuildAsync_RefreshPastCooldown_Rebuilds()
    {
        var store = CreateStore();
        await store.SetAsync(Key, new AggregatedRegistryResponse
        {
            GeneratedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        using var sut = CreateSut(store);
        var buildCount = 0;

        await sut.GetOrBuildAsync(
            ttl: null,
            refreshCooldown: TimeSpan.FromSeconds(30),
            forceRefresh: true,
            build: _ =>
            {
                buildCount++;
                return Clean();
            },
            cancellationToken: default);

        Assert.Equal(1, buildCount);
    }

    // IRegistrySnapshotStore is the public seam for a shared store (#293): two caches over one
    // store (e.g. two router replicas) serve one snapshot, built once.
    [Fact]
    public async Task GetOrBuildAsync_TwoCachesOverOneStore_ShareTheSnapshot()
    {
        var store = CreateStore();
        using var first = CreateSut(store);
        using var second = CreateSut(store);
        var builds = 0;

        await first.GetOrBuildAsync(null, TimeSpan.Zero, false, _ => { builds++; return Clean(); }, TestContext.Current.CancellationToken);
        await second.GetOrBuildAsync(null, TimeSpan.Zero, false, _ => { builds++; return Clean(); }, TestContext.Current.CancellationToken);

        Assert.Equal(1, builds);
    }

    [Fact]
    public async Task GetOrBuildAsync_PassesTheCallersTokenToTheStore()
    {
        using var cts = new CancellationTokenSource();
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(Key, cts.Token)).ReturnsAsync((AggregatedRegistryResponse?)null);
        using var sut = CreateSut(store.Object);

        await sut.GetOrBuildAsync(null, TimeSpan.Zero, false, _ => Clean(), cts.Token);

        store.Verify(s => s.GetAsync(Key, cts.Token), Times.Exactly(2));
        store.Verify(s => s.SetAsync(Key, It.IsAny<AggregatedRegistryResponse>(), cts.Token), Times.Once);
    }

    // A store failure is the caller's failure: nothing is built on top of an unreadable store.
    // A store that may be unavailable handles that itself (a distributed store falls back, #275).
    [Fact]
    public async Task GetOrBuildAsync_WhenTheStoreFails_TheFailurePropagates_AndNothingIsBuilt()
    {
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(Key, It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("store down"));
        using var sut = CreateSut(store.Object);
        var built = false;

        await Assert.ThrowsAsync<TimeoutException>(() => sut.GetOrBuildAsync(null, TimeSpan.Zero, false, _ => { built = true; return Clean(); }, TestContext.Current.CancellationToken));

        Assert.False(built);
    }

    [Fact]
    public async Task GetOrBuildAsync_WhenCanceled_StopsWithoutStoring()
    {
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(Key, It.IsAny<CancellationToken>())).ReturnsAsync((AggregatedRegistryResponse?)null);
        using var sut = CreateSut(store.Object);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.GetOrBuildAsync(
            null,
            TimeSpan.Zero,
            false,
            ct =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Clean();
            },
            cts.Token));

        store.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AggregatedRegistryResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

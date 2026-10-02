using Kaleido.Http.Registry;

namespace Kaleido.Http.UnitTests.Registry;

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
}

using Kaleido.Http.Registry;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class RegistrySnapshotTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Dictionary<string, AggregatedRegistryResponse> stored = new(StringComparer.OrdinalIgnoreCase);

    private RegistrySnapshot CreateSut(string serviceName = "router")
    {
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string k, CancellationToken _) => new ValueTask<AggregatedRegistryResponse?>(stored.TryGetValue(k, out var v) ? v : null));
        store.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AggregatedRegistryResponse>(), It.IsAny<CancellationToken>()))
            .Returns((string k, AggregatedRegistryResponse v, CancellationToken _) =>
            {
                stored[k] = v;
                return ValueTask.CompletedTask;
            });

        return new RegistrySnapshot(store.Object, new KaleidoServiceOptions { ServiceName = serviceName });
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public void BeforeConfigure_IsNotMapped_AndHasTheDefaults()
    {
        using var sut = CreateSut();

        Assert.False(sut.IsMapped);
        Assert.False(sut.Aggregate);
        Assert.Null(sut.CacheTtl);
        Assert.Equal(TimeSpan.FromSeconds(30), sut.RefreshCooldown);
        Assert.Null(sut.DownstreamMaxParallelism);
    }

    [Fact]
    public void Configure_TakesTheMapOptions()
    {
        using var sut = CreateSut();

        sut.Configure(new KaleidoHttpMapOptions
        {
            AggregateRegistry = true,
            RegistryCacheTtl = TimeSpan.FromMinutes(5),
            RegistryRefreshCooldown = TimeSpan.FromSeconds(3),
            RegistryDownstreamMaxParallelism = 4
        });

        Assert.True(sut.IsMapped);
        Assert.True(sut.Aggregate);
        Assert.Equal(TimeSpan.FromMinutes(5), sut.CacheTtl);
        Assert.Equal(TimeSpan.FromSeconds(3), sut.RefreshCooldown);
        Assert.Equal(4, sut.DownstreamMaxParallelism);
    }

    [Fact]
    public async Task GetOrBuild_StoresUnderTheServicesCanonicalKey_AndServesItAfterwards()
    {
        using var sut = CreateSut("router");
        sut.Configure(new KaleidoHttpMapOptions());
        var builds = 0;

        Task<AggregatedRegistryResponse> Build(CancellationToken _)
        {
            builds++;
            return Task.FromResult(new AggregatedRegistryResponse());
        }

        await sut.GetOrBuildAsync(refresh: false, Build, Cancel);
        await sut.GetOrBuildAsync(refresh: false, Build, Cancel);

        Assert.Equal(1, builds);
        Assert.True(stored.ContainsKey("kaleido:router"));
    }
}

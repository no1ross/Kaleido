using Kaleido.Platform.Registry;

namespace Kaleido.Http.Server.Registry.UnitTests;

public sealed class RegistrySnapshotTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IHttpRegistryCache> cache = new();

    private RegistrySnapshot CreateSut(string serviceName = "router", string? serviceVersion = null) =>
        new(cache.Object, new KaleidoServiceOptions { ServiceName = serviceName, ServiceVersion = serviceVersion });

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public void BeforeConfigure_IsNotMapped_AndHasTheDefaults()
    {
        var sut = CreateSut();

        Assert.False(sut.IsMapped);
        Assert.False(sut.Aggregate);
        Assert.Null(sut.CacheTtl);
        Assert.Equal(TimeSpan.FromSeconds(30), sut.RefreshCooldown);
        Assert.Null(sut.DownstreamMaxParallelism);
    }

    [Fact]
    public void Configure_TakesTheMapOptions()
    {
        var sut = CreateSut();

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
    public async Task GetOrBuild_AsksTheCacheUnderTheServicesCanonicalKey_WithTheMappedSettings()
    {
        var sut = CreateSut("router");
        sut.Configure(new KaleidoHttpMapOptions
        {
            RegistryCacheTtl = TimeSpan.FromMinutes(5),
            RegistryRefreshCooldown = TimeSpan.FromSeconds(3)
        });
        var built = new KaleidoRegistry();
        cache.Setup(c => c.GetOrBuildAsync("kaleido:router", TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(3), true, It.IsAny<Func<CancellationToken, Task<KaleidoRegistry>>>(), Cancel))
            .ReturnsAsync(built);

        var result = await sut.GetOrBuildAsync(refresh: true, _ => Task.FromResult(new KaleidoRegistry()), Cancel);

        Assert.Same(built, result);
    }

    // #552: versions of one service sharing a cache keep their own snapshot.
    [Fact]
    public async Task GetOrBuild_ForAVersionedService_UsesTheVersionsOwnKey()
    {
        var sut = CreateSut("router", serviceVersion: "2");
        sut.Configure(new KaleidoHttpMapOptions());
        var built = new KaleidoRegistry();
        cache.Setup(c => c.GetOrBuildAsync("kaleido:router@2", It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan>(), false, It.IsAny<Func<CancellationToken, Task<KaleidoRegistry>>>(), Cancel))
            .ReturnsAsync(built);

        var result = await sut.GetOrBuildAsync(refresh: false, _ => Task.FromResult(new KaleidoRegistry()), Cancel);

        Assert.Same(built, result);
    }
}

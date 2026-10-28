namespace Kaleido.Http.Registry.UnitTests;

public sealed class RegistrySnapshotTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IHttpRegistryCache> cache = new();

    private RegistrySnapshot CreateSut(string serviceName = "router") =>
        new(cache.Object, new KaleidoServiceOptions { ServiceName = serviceName });

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
        var built = new AggregatedRegistryResponse();
        cache.Setup(c => c.GetOrBuildAsync("kaleido:router", TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(3), true, It.IsAny<Func<CancellationToken, Task<AggregatedRegistryResponse>>>(), Cancel))
            .ReturnsAsync(built);

        var result = await sut.GetOrBuildAsync(refresh: true, _ => Task.FromResult(new AggregatedRegistryResponse()), Cancel);

        Assert.Same(built, result);
    }
}

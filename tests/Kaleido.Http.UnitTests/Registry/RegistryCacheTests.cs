using Kaleido.Http.Registry;

namespace Kaleido.Http.UnitTests.Registry;

public sealed class HttpRegistryCacheTests
{
    [Fact]
    public void Current_WhenNothingCached_ReturnsNull()
    {
        using var sut = new HttpRegistryCache();
        Assert.Null(sut.Current);
    }

    [Fact]
    public async Task GetOrBuildAsync_WhenNoCachedResult_InvokesBuild()
    {
        using var sut = new HttpRegistryCache();
        var built = new AggregatedRegistryResponse();
        var buildCalled = false;

        var result = await sut.GetOrBuildAsync(
            forceRefresh: false,
            build: _ =>
            {
                buildCalled = true;
                return Task.FromResult(built);
            },
            cancellationToken: default);

        Assert.True(buildCalled);
        Assert.Same(built, result);
    }

    [Fact]
    public async Task GetOrBuildAsync_WhenCachedAndNoForceRefresh_ReturnsCachedResult()
    {
        using var sut = new HttpRegistryCache();
        var first = new AggregatedRegistryResponse();

        // Prime the cache with a clean (no client errors) result.
        await sut.GetOrBuildAsync(false, _ => Task.FromResult(first), default);

        var buildCount = 0;
        var result = await sut.GetOrBuildAsync(
            forceRefresh: false,
            build: _ =>
            {
                buildCount++;
                return Task.FromResult(new AggregatedRegistryResponse());
            },
            cancellationToken: default);

        Assert.Equal(0, buildCount);
        Assert.Same(first, result);
    }
}

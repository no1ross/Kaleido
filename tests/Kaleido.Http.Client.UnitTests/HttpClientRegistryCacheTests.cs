using Kaleido.Http.Client;

namespace Kaleido.Http.Client.UnitTests;

public sealed class HttpClientRegistryCacheTests
    : Kaleido.UnitTests.SutFixture
{
    private static HttpClientRegistryCache<T> CreateSut<T>()
        where T : class =>
        new();

    [Fact]
    public async Task GetOrFetchAsync_WhenNothingCached_InvokesFetch()
    {
        using var sut = CreateSut<string>();
        var fetchCalled = false;

        var result = await sut.GetOrFetchAsync(
            _ =>
            {
                fetchCalled = true;
                return Task.FromResult("value");
            },
            CancellationToken.None);

        Assert.True(fetchCalled);
        Assert.Equal("value", result);
    }

    [Fact]
    public async Task GetOrFetchAsync_WhenAlreadyCached_DoesNotInvokeFetchAgain()
    {
        using var sut = CreateSut<string>();

        // Prime the cache.
        await sut.GetOrFetchAsync(_ => Task.FromResult("first"), CancellationToken.None);

        var fetchCount = 0;
        var result = await sut.GetOrFetchAsync(
            _ =>
            {
                fetchCount++;
                return Task.FromResult("second");
            },
            CancellationToken.None);

        Assert.Equal(0, fetchCount);
        Assert.Equal("first", result);
    }

    [Fact]
    public async Task Reset_ClearsCache_SoNextFetchIsInvoked()
    {
        using var sut = CreateSut<string>();

        // Prime the cache.
        await sut.GetOrFetchAsync(_ => Task.FromResult("first"), CancellationToken.None);

        // Invalidate.
        sut.Reset();

        // Next call should re-fetch.
        var fetchCalled = false;
        var result = await sut.GetOrFetchAsync(
            _ =>
            {
                fetchCalled = true;
                return Task.FromResult("refreshed");
            },
            CancellationToken.None);

        Assert.True(fetchCalled);
        Assert.Equal("refreshed", result);
    }
}

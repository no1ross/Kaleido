namespace Kaleido.Http.Registry.UnitTests;

public sealed class InMemoryRegistrySnapshotStoreTests
    : Kaleido.UnitTests.SutFixture
{
    private static InMemoryRegistrySnapshotStore CreateSut() =>
        new();

    [Fact]
    public async Task GetAsync_WhenKeyAbsent_ReturnsNull()
    {
        var store = CreateSut();

        var result = await store.GetAsync("missing", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGet_ReturnsSnapshot()
    {
        var store = CreateSut();

        var snapshot = new AggregatedRegistryResponse
        {
            GeneratedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        await store.SetAsync("kaleido:member", snapshot, TestContext.Current.CancellationToken);
        var result = await store.GetAsync("kaleido:member", TestContext.Current.CancellationToken);

        Assert.Same(snapshot, result);
    }

    [Fact]
    public async Task RemoveAsync_RemovesSnapshot()
    {
        var store = CreateSut();

        var snapshot = new AggregatedRegistryResponse();

        await store.SetAsync("kaleido:member", snapshot, TestContext.Current.CancellationToken);
        await store.RemoveAsync("kaleido:member", TestContext.Current.CancellationToken);

        Assert.Null(await store.GetAsync("kaleido:member", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Keys_AreCaseInsensitive()
    {
        var store = CreateSut();

        var snapshot = new AggregatedRegistryResponse();

        await store.SetAsync("Kaleido:Member", snapshot, TestContext.Current.CancellationToken);

        Assert.Same(snapshot, await store.GetAsync("kaleido:member", TestContext.Current.CancellationToken));
    }
}

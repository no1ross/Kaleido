using Kaleido.Http.Registry;

namespace Kaleido.Http.Abstractions.UnitTests.Registry;

public sealed class InMemoryRegistrySnapshotStoreTests
    : Kaleido.UnitTests.SutFixture<InMemoryRegistrySnapshotStore>
{
    protected override InMemoryRegistrySnapshotStore CreateSut() =>
        new();

    [Fact]
    public async Task GetAsync_WhenKeyAbsent_ReturnsNull()
    {
        var store = Sut;

        var result = await store.GetAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGet_ReturnsSnapshot()
    {
        var store = Sut;

        var snapshot = new AggregatedRegistryResponse
        {
            GeneratedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        await store.SetAsync("kaleido:member", snapshot);
        var result = await store.GetAsync("kaleido:member");

        Assert.Same(snapshot, result);
    }

    [Fact]
    public async Task RemoveAsync_RemovesSnapshot()
    {
        var store = Sut;

        var snapshot = new AggregatedRegistryResponse();

        await store.SetAsync("kaleido:member", snapshot);
        await store.RemoveAsync("kaleido:member");

        Assert.Null(await store.GetAsync("kaleido:member"));
    }

    [Fact]
    public async Task Keys_AreCaseInsensitive()
    {
        var store = Sut;

        var snapshot = new AggregatedRegistryResponse();

        await store.SetAsync("Kaleido:Member", snapshot);

        Assert.Same(snapshot, await store.GetAsync("kaleido:member"));
    }
}

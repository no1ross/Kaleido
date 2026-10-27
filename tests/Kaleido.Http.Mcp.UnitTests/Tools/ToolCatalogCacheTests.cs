namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class ToolCatalogCacheTests
    : Kaleido.UnitTests.SutFixture
{
    private static ToolCatalogCache CreateSut() => new();

    private static ToolCatalog Catalog(string revision) => new() { Revision = revision };

    [Fact]
    public void TryGet_AKeptKey_ReturnsItsCatalog()
    {
        var sut = CreateSut();
        var catalog = Catalog("r-1");
        sut.Add("r-1", catalog);

        Assert.True(sut.TryGet("r-1", out var found));
        Assert.Same(catalog, found);
    }

    [Fact]
    public void TryGet_AnUnknownKey_ReturnsNothing()
    {
        Assert.False(CreateSut().TryGet("r-1", out var found));
        Assert.Null(found);
    }

    [Fact]
    public void Add_AKnownKey_ReplacesItsCatalog_WithoutGrowing()
    {
        var sut = CreateSut();
        sut.Add("r-1", Catalog("old"));
        var newer = Catalog("new");

        sut.Add("r-1", newer);

        Assert.True(sut.TryGet("r-1", out var found));
        Assert.Same(newer, found);
        Assert.Equal(1, sut.Count);
    }

    // #464: at capacity one catalog goes - the least recently used - never all of them.
    [Fact]
    public void Add_AtCapacity_DropsOnlyTheLeastRecentlyUsed()
    {
        var sut = CreateSut();
        for (var i = 0; i < ToolCatalogCache.Capacity; i++)
        {
            sut.Add($"r-{i}", Catalog($"r-{i}"));
        }

        sut.Add("r-new", Catalog("r-new"));

        Assert.Equal(ToolCatalogCache.Capacity, sut.Count);
        Assert.False(sut.TryGet("r-0", out _));
        Assert.True(sut.TryGet("r-1", out _));
        Assert.True(sut.TryGet("r-new", out _));
    }

    // Reading a catalog makes it the most recently used, so it outlives catalogs nobody asked for.
    [Fact]
    public void TryGet_KeepsACatalogInUse_WhenTheCacheIsFull()
    {
        var sut = CreateSut();
        for (var i = 0; i < ToolCatalogCache.Capacity; i++)
        {
            sut.Add($"r-{i}", Catalog($"r-{i}"));
        }

        Assert.True(sut.TryGet("r-0", out _));
        sut.Add("r-new", Catalog("r-new"));

        Assert.True(sut.TryGet("r-0", out _));
        Assert.False(sut.TryGet("r-1", out _));
    }

    [Fact]
    public async Task ConcurrentUse_NeverExceedsTheCapacity()
    {
        var sut = CreateSut();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < ToolCatalogCache.Capacity; i++)
            {
                sut.Add($"w{worker}-{i}", Catalog("r"));
                sut.TryGet($"w{worker}-{i / 2}", out _);
            }
        }, TestContext.Current.CancellationToken)));

        Assert.Equal(ToolCatalogCache.Capacity, sut.Count);
    }
}

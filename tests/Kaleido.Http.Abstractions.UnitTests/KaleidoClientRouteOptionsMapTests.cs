namespace Kaleido.Http.UnitTests;

public sealed class KaleidoClientRouteOptionsMapTests
    : Kaleido.UnitTests.SutFixture
{
    private static TestRouteOptionsMap CreateSut() =>
        new TestRouteOptionsMap();

    [Fact]
    public void NewMap_HasNoEntries()
    {
        var sut = CreateSut();

        Assert.Empty(sut.Options);
        Assert.Empty(sut.RegistryTtls);
    }

    [Fact]
    public void SetRoutePrefix_ForAnExistingClientInAnyCase_ReplacesThePrefix()
    {
        var sut = CreateSut();

        sut.SetRoutePrefix("Orders", "orders-v1");
        sut.SetRoutePrefix("orders", "orders-v2");

        var entry = Assert.Single(sut.Options);
        Assert.Equal("Orders", entry.Key);
        Assert.Equal("orders-v2", sut.Options["ORDERS"]);
        Assert.Empty(sut.RegistryTtls);
    }

    [Fact]
    public void SetRegistryTtl_ForAnExistingClientInAnyCase_ReplacesTheTtl()
    {
        var sut = CreateSut();

        sut.SetRegistryTtl("Orders", TimeSpan.FromMinutes(1));
        sut.SetRegistryTtl("orders", TimeSpan.FromMinutes(5));

        Assert.Single(sut.RegistryTtls);
        Assert.Equal(TimeSpan.FromMinutes(5), sut.RegistryTtls["ORDERS"]);
        Assert.Empty(sut.Options);
    }

    private sealed class TestRouteOptionsMap : KaleidoClientRouteOptionsMap;
}

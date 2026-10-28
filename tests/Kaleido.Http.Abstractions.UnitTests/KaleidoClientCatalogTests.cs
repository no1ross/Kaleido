namespace Kaleido.Http.UnitTests;

public sealed class KaleidoClientCatalogTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoClientCatalog CreateSut(params KaleidoClientDefinition[] definitions) =>
        new(definitions);

    private static KaleidoClientDefinition Client(string name, string routePrefix) =>
        new(name, routePrefix, RegistryTtl: null, StrictRegistryProbe: false, HealthCheckTimeout: TimeSpan.FromSeconds(5));

    [Fact]
    public void NoDefinitions_HasNoClients()
    {
        var sut = CreateSut();

        Assert.Empty(sut.Clients);
        Assert.Null(sut.Find("Orders"));
        Assert.Null(sut.FindByService("orders"));
    }

    [Fact]
    public void Find_MatchesTheNameCaseInsensitively()
    {
        var orders = Client("Orders", "orders");

        Assert.Same(orders, CreateSut(orders).Find("ORDERS"));
    }

    [Fact]
    public void FindByService_MatchesTheRoutePrefixCaseInsensitively()
    {
        var orders = Client("OrdersClient", "orders");

        Assert.Same(orders, CreateSut(Client("Billing", "billing"), orders).FindByService("Orders"));
    }

    // AddHttpClients() registers one definition per name; a name defined again (in any case) is the
    // later definition.
    [Fact]
    public void ANameDefinedTwice_KeepsTheLaterDefinition()
    {
        var later = Client("orders", "orders-v2");

        var sut = CreateSut(Client("Orders", "orders-v1"), later);

        Assert.Same(later, Assert.Single(sut.Clients));
        Assert.Same(later, sut.Find("Orders"));
    }
}

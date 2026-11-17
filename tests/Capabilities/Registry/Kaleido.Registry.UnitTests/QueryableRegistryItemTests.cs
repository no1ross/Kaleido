namespace Kaleido.Registry.UnitTests;

public sealed class QueryableRegistryItemTests : Kaleido.UnitTests.SutFixture<QueryableRegistryItem>
{
    protected override QueryableRegistryItem CreateSut() => new()
    {
        ServiceName = "catalog",
        ServiceVersion = "1",
        Name = "orders",
        Bindings = new RegistryTransportBindings
        {
            Http = [new RegistryHttpOperation
            {
                Operation = "query",
                Method = "POST",
                Path = "/catalog/1/queryable/orders/query",
            }],
        },
    };

    [Fact]
    public void Bindings_KeepCallableOperationsSeparateFromCapabilityMetadata()
    {
        var item = Sut;

        Assert.Equal("orders", item.Name);
        Assert.Equal("/catalog/1/queryable/orders/query", Assert.Single(item.Bindings.Http).Path);
        Assert.Equal(RegistryStatusNames.NotReported, item.Status);
        Assert.DoesNotContain(typeof(QueryableRegistryItem).GetProperties(), property => property.Name == "QueryUrl");
        Assert.DoesNotContain(typeof(QueryableRegistryItem).GetProperties(), property => property.PropertyType == typeof(Type));
    }
}

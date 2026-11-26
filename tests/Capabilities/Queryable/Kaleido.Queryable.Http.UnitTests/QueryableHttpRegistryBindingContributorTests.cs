using Kaleido.Registry;

namespace Kaleido.Queryable.Http.UnitTests;

public sealed class QueryableHttpRegistryBindingContributorTests : Kaleido.UnitTests.SutFixture
{
    private static QueryableHttpRegistryBindingContributor CreateSut() => new();

    [Fact]
    public void Bind_AddsOnlyHttpQueryOperationsToThePublishedSourceAndViews()
    {
        var published = new QueryableRegistryItem
        {
            ServiceName = "catalog",
            Name = "CatalogSource",
            Description = "Catalog entries",
            Views = [new QueryableRegistryViewItem { Name = "RecentView" }],
        };
        var service = new KaleidoServiceOptions { ServiceName = "catalog", ServiceVersion = "1" };

        var result = CreateSut().Bind(published, service);

        Assert.Equal("Catalog entries", result.Description);
        var source = Assert.Single(result.Bindings.Http);
        Assert.Equal(("query", "POST", "/catalog/1/queryable/catalogsource/query"), (source.Operation, source.Method, source.Path));
        var view = Assert.Single(Assert.Single(result.Views).Bindings.Http);
        Assert.Equal("/catalog/1/queryable/catalogsource/recentview/query", view.Path);
        Assert.Empty(published.Bindings.Http);
        Assert.Empty(Assert.Single(published.Views).Bindings.Http);
    }
}

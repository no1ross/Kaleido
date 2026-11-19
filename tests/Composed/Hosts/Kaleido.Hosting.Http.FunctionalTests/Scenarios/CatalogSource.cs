namespace Kaleido.Hosting.Http.FunctionalTests.Scenarios;

[QuerySource(DisplayName = "Catalog", Description = "Read local test catalog records")]
[KaleidoAuthorization(Roles = "reader")]
public sealed class CatalogSource : IQuerySource<CatalogRecord>
{
    public IQueryable<CatalogRecord> CreateQuery(QueryExecutionContext executionContext) =>
        new[] { new CatalogRecord { Name = "one" } }.AsQueryable();
}

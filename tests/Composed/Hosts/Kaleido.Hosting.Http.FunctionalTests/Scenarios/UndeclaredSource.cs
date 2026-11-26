namespace Kaleido.Hosting.Http.FunctionalTests.Scenarios;

[QuerySource(DisplayName = "Undeclared source", Description = "A source with no explicit authorization rule")]
public sealed class UndeclaredSource : IQuerySource<CatalogRecord>
{
    public IQueryable<CatalogRecord> CreateQuery(QueryExecutionContext executionContext) =>
        new[] { new CatalogRecord { Name = "unprotected" } }.AsQueryable();
}

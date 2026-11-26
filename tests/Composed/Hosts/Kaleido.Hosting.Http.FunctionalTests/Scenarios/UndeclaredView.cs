namespace Kaleido.Hosting.Http.FunctionalTests.Scenarios;

[QueryView(DisplayName = "Undeclared view", Description = "A view with no explicit authorization rule")]
public sealed class UndeclaredView : IQueryViewSource<UndeclaredSource, CatalogRecord, CatalogRecord>
{
    public IQueryable<CatalogRecord> CreateView(IQueryable<CatalogRecord> query, QueryExecutionContext executionContext) => query;
}

namespace Kaleido.Hosting.Http.FunctionalTests.Scenarios;

[QueryView(DisplayName = "Visible view", Description = "A view declared on an otherwise hidden source")]
[KaleidoAuthorization(Roles = "viewer")]
public sealed class VisibleView : IQueryViewSource<UndeclaredSource, CatalogRecord, CatalogRecord>
{
    public IQueryable<CatalogRecord> CreateView(IQueryable<CatalogRecord> query, QueryExecutionContext executionContext) => query;
}

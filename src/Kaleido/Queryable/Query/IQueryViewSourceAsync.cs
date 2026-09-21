namespace Kaleido.Queryable.Query;

public interface IQueryViewSourceAsync<TQueryContext, TView, TViewParameters>
    where TQueryContext : class
    where TView : class
    where TViewParameters : class
{
    Task<IQueryable<TView>> CreateViewAsync(
        IQueryable<TQueryContext> query,
        QueryExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}

public interface IQueryViewSourceAsync<TQueryContext, TView>
    : IQueryViewSourceAsync<TQueryContext, TView, EmptyQueryViewParameters>
    where TQueryContext : class
    where TView : class
{
}

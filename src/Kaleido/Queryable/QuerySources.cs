namespace Kaleido.Queryable;

public interface IQueryContextSource<TQueryContext>
    where TQueryContext : class
{
    IQueryable<TQueryContext> CreateQuery(QueryExecutionContext executionContext);
}

public interface IQueryContextSourceAsync<TQueryContext>
    where TQueryContext : class
{
    Task<IQueryable<TQueryContext>> CreateQueryAsync(
        QueryExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}

[ExcludeFromCodeCoverage]
public sealed record EmptyQueryViewParameters;

public interface IQueryViewSource<TQueryView, TView, TViewParameters>
    where TQueryView : class
    where TView : class
    where TViewParameters : class
{
    IQueryable<TView> CreateView(IQueryable<TQueryView> query, QueryExecutionContext executionContext);
}

public interface IQueryViewSource<TQueryView, TView> : IQueryViewSource<TQueryView, TView, EmptyQueryViewParameters>
    where TQueryView : class
    where TView : class
{
}

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

public interface IDelegateQueryViewSource<TDelegateContext, TView, TViewParameters>
    where TDelegateContext : class
    where TView : class
    where TViewParameters : class
{
    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest<TViewParameters> request,
        CancellationToken cancellationToken = default);
}

public interface IDelegateQueryViewSource<TDelegateContext, TView>
    : IDelegateQueryViewSource<TDelegateContext, TView, EmptyQueryViewParameters>
    where TDelegateContext : class
    where TView : class
{
}

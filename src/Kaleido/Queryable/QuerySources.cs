namespace Kaleido.Queryable;

/// <summary>
/// Synchronous context source — implement when the underlying queryable is
/// already materialized or cheap to produce (in-memory, pre-fetched data,
/// or a LINQ provider that defers execution). If producing the queryable
/// requires I/O, implement <see cref="IQueryContextSourceAsync{TQueryContext}"/>
/// instead — do not block inside <see cref="CreateQuery"/>.
/// </summary>
/// <remarks>
/// For AI/code generators: prefer this interface unless the source performs
/// async I/O. Returning a completed task from the sync path is fine; calling
/// <c>.Result</c>/<c>.GetAwaiter().GetResult()</c> in a sync implementation is
/// not.
/// </remarks>
public interface IQueryContextSource<TQueryContext>
    where TQueryContext : class
{
    IQueryable<TQueryContext> CreateQuery(QueryExecutionContext executionContext);
}

/// <summary>
/// Asynchronous context source — implement when producing the queryable
/// requires async I/O (remote fetch, async seed, async auth resolution).
/// If the queryable is already in hand, implement the synchronous
/// <see cref="IQueryContextSource{TQueryContext}"/> instead.
/// </summary>
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

public interface IDelegatedQueryViewSource<TDelegateContext, TView, TViewParameters>
    where TDelegateContext : class
    where TView : class
    where TViewParameters : class
{
    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest<TViewParameters> request,
        CancellationToken cancellationToken = default);
}

public interface IDelegatedQueryViewSource<TDelegateContext, TView>
    : IDelegatedQueryViewSource<TDelegateContext, TView, EmptyQueryViewParameters>
    where TDelegateContext : class
    where TView : class
{
}

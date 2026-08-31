namespace Kaleido.Queryable.Query;

public interface IQueryContextSource<TQueryContext>
        where TQueryContext : class
{
    IQueryable<TQueryContext> CreateQuery(QueryExecutionContext executionContext);
}

public interface IDelegatedQueryContextSource<TQueryContext, TView>
        where TQueryContext : class
        where TView : class
{
    Task<QueryResult<TView>> ExecuteAsync(
        IQueryRequest request,
        CancellationToken cancellationToken = default);
}

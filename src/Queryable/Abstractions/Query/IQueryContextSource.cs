namespace Kaleido.Queryable.Query;

public interface IQueryContextSource<TQueryContext>
        where TQueryContext : class
{
    IQueryable<TQueryContext> CreateQuery(QueryExecutionContext executionContext);
}

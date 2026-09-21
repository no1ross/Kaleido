namespace Kaleido.Queryable.Query;

public interface IQueryContextSourceAsync<TQueryContext>
    where TQueryContext : class
{
    Task<IQueryable<TQueryContext>> CreateQueryAsync(
        QueryExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}

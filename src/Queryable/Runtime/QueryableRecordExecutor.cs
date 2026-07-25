namespace Kaleido.Queryable.Runtime;

public sealed class QueryableRecordExecutor<TRecord> : IQueryableRecordExecutor<TRecord>
    where TRecord : class
{
    public Task<int> CountAsync(IQueryable<TRecord> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(query.Count());
    }

    public Task<IReadOnlyList<TRecord>> ToListAsync(IQueryable<TRecord> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<TRecord>>(query.ToList());
    }
}

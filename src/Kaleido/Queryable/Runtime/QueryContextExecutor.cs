using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Runtime;

internal sealed class QueryContextExecutor<TView> : IQueryContextExecutor<TView>
    where TView : class
{
    public Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(query.Count());
    }

    public Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<TView>>(query.ToList());
    }


    public IQueryable<TView> ApplyPage(
        IQueryable<TView> query,
        CompiledPage page)
    {
        return query
            .Skip(page.Offset)
            .Take(page.Size);
    }

}

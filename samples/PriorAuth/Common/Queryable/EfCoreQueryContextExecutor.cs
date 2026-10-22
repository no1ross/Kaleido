using Kaleido.Queryable.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Kaleido.Samples.PriorAuth.Queryable;

/// <summary>
/// Runs Kaleido's queries with EF Core's own async operators, so a full page is counted with one
/// <c>SELECT COUNT(*)</c> instead of reading every matching row (docs/PATTERNS.md, #352). It serves every
/// source of the host: a query that is not EF Core's (a delegated or in-memory source) uses LINQ.
/// </summary>
internal sealed class EfCoreQueryContextExecutor<TView> : IQueryContextExecutor<TView>
    where TView : class
{
    public Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken) =>
        query.Provider is IAsyncQueryProvider
            ? EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken)
            : Task.FromResult(query.Count());

    public async Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken) =>
        query.Provider is IAsyncQueryProvider
            ? await EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken)
            : query.ToList();

    public IQueryable<TView> ApplyPage(IQueryable<TView> query, int size, int offset) =>
        query.Skip(offset).Take(size);
}

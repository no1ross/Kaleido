using Kaleido.Queryable.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Kaleido.Samples.ECommerce.Data;

/// <summary>
/// Runs Kaleido's queries with EF Core's own async operators, so a full page is counted with one
/// <c>SELECT COUNT(*)</c> instead of reading every matching row (docs/PATTERNS.md, #352). This host also
/// has in-memory views (the shopping-cart views): a query that is not EF Core's uses LINQ, since EF's
/// async operators throw on it.
/// </summary>
public sealed class EfCoreQueryContextExecutor<TView> : IQueryContextExecutor<TView>
    where TView : class
{
    /// <inheritdoc />
    public Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken = default) =>
        query.Provider is IAsyncQueryProvider
            ? EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken)
            : Task.FromResult(query.Count());

    /// <inheritdoc />
    public async Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken = default) =>
        query.Provider is IAsyncQueryProvider
            ? await EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken)
            : query.ToList();

    /// <inheritdoc />
    public IQueryable<TView> ApplyPage(IQueryable<TView> query, int size, int offset) =>
        query.Skip(offset).Take(size);
}

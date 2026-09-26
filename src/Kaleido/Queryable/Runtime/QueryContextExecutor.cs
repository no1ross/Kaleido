using System.Runtime.CompilerServices;

namespace Kaleido.Queryable.Runtime;

/// <summary>
/// Executes terminal LINQ operators (<c>Count</c>, <c>ToList</c>) and paging
/// against a queryable view.
/// </summary>
/// <remarks>
/// This is the intended extension point for async-capable providers: consumers
/// backed by EF Core should register their own implementation that calls the
/// provider's true async operators (<c>EntityFrameworkCore.CountAsync</c>,
/// <c>ToListAsync</c>) so queries stay off the request thread. The default
/// implementation uses <see cref="IAsyncEnumerable{T}"/> when the queryable
/// supports it and falls back to synchronous LINQ otherwise.
/// </remarks>
public interface IQueryContextExecutor<TView>
    where TView : class
{
    /// <summary>Counts the elements of <paramref name="query"/> asynchronously.</summary>
    Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken = default);

    /// <summary>Materializes <paramref name="query"/> to a list asynchronously.</summary>
    Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken = default);

    /// <summary>Applies <paramref name="offset"/>/<paramref name="size"/> paging to <paramref name="query"/>.</summary>
    IQueryable<TView> ApplyPage(IQueryable<TView> query, int size, int offset);
}

internal sealed class QueryContextExecutor<TView> : IQueryContextExecutor<TView>
    where TView : class
{
    public async Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);

        if (query is IAsyncEnumerable<TView> asyncQuery)
        {
            var count = 0;

            await foreach (var _ in Enumerate(asyncQuery, cancellationToken))
            {
                count++;
            }

            return count;
        }

        return query.Count();
    }

    public async Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);

        if (query is IAsyncEnumerable<TView> asyncQuery)
        {
            var list = new List<TView>();

            await foreach (var item in Enumerate(asyncQuery, cancellationToken))
            {
                list.Add(item);
            }

            return list;
        }

        return query.ToList();
    }

    public IQueryable<TView> ApplyPage(
        IQueryable<TView> query,
        int size,
        int offset)
    {
        return query
            .Skip(offset)
            .Take(size);
    }

    private static async IAsyncEnumerable<TView> Enumerate(
        IAsyncEnumerable<TView> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}

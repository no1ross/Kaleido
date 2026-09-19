using Kaleido.Queryable.Query;
using System.Security.Cryptography;

namespace Kaleido.Queryable.Runtime;

internal interface IQueryContextExecutor<TView>
    where TView : class
{
    Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken = default);
    IQueryable<TView> ApplyPage(IQueryable<TView> query, CompiledPage page);
}
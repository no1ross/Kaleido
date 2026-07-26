using System.Security.Cryptography;

namespace Kaleido.Queryable.Runtime
{
    public interface IRecordExecutor<TRecord>
        where TRecord : class
    {
        Task<int> CountAsync(IQueryable<TRecord> query, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TRecord>> ToListAsync(IQueryable<TRecord> query, CancellationToken cancellationToken = default);
    }
}
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable
{
    public interface IQueryableService
    {
        Task<QueryResult<TRecord>> QueryAsync<TRecord>(string recordKey, QueryRequest request, CancellationToken cancellationToken = default) where TRecord : class;
    }
}
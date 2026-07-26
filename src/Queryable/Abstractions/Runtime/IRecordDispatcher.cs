using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Runtime
{
    public interface IRecordDispatcher
    {
        Task<QueryResponse<TRecord>> DispatchAsync<TRecord>(string recordKey, QueryRequest request, CancellationToken cancellationToken = default) where TRecord : class;
    }
}
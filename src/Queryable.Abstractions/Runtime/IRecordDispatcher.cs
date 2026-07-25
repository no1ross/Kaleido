using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Runtime
{
    public interface IRecordDispatcher
    {
        Task<KaleidoQueryResponse<TRecord>> DispatchAsync<TRecord>(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default) where TRecord : class;
    }
}
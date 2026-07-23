namespace Kaleido.Queryable
{
    public interface IRecordDispatcher
    {
        Task<KaleidoQueryResponse<TRecord>> DispatchAsync<TRecord>(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default) where TRecord : class;
    }
}
namespace Kaleido
{
    public interface IRecordDispatcher
    {
        Task<KaleidoQueryResponse> DispatchAsync(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default);
        Task<KaleidoQueryResponse<TRecord>> DispatchAsync<TRecord>(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default) where TRecord : class;
    }
}
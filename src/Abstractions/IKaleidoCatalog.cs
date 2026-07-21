namespace Kaleido
{
    /// <summary>Primary consumer-facing facade for metadata discovery and query execution.</summary>
    public interface IKaleidoCatalog
    {
        /// <summary>Returns descriptors for all registered records.</summary>
        /// <returns>Registered record descriptors.</returns>
        IReadOnlyCollection<RecordDescriptor> GetAll();

        /// <summary>Returns one registered record descriptor.</summary>
        /// <param name="recordKey">Logical key defined by <see cref="KaleidoRecordAttribute"/>.</param>
        /// <returns>The descriptor, or null when the record is not registered.</returns>
        RecordDescriptor? Get(string recordKey);

        /// <summary>Executes a query against a registered record.</summary>
        /// <param name="recordKey">Logical key defined by <see cref="KaleidoRecordAttribute"/>.</param>
        /// <param name="request">Query request containing optional named query, filters, search, sort, and paging.</param>
        /// <param name="cancellationToken">Token used to cancel query execution.</param>
        /// <returns>Query response containing metadata and matching items.</returns>
        Task<KaleidoQueryResponse> QueryAsync(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default);

        /// <summary>Executes a query against a registered record.</summary>
        /// <param name="recordKey">Logical key defined by <see cref="KaleidoRecordAttribute"/>.</param>
        /// <param name="request">Query request containing optional named query, filters, search, sort, and paging.</param>
        /// <param name="cancellationToken">Token used to cancel query execution.</param>
        /// <returns>Query response containing metadata and matching items.</returns>
        Task<KaleidoQueryResponse<TRecord>> QueryAsync<TRecord>(string recordKey, KaleidoQueryRequest request, CancellationToken cancellationToken = default) where TRecord : class;
    }
}
namespace Kaleido
{
    /// <summary>
    /// Coordinates the complete record query pipeline.
    ///
    /// Execution Flow:
    ///     1. Retrieve metadata
    ///     2. Validate request
    ///     3. Compile request
    ///     4. Create source query
    ///     5. Apply named query
    ///     6. Apply compiled query
    ///     7. Materialize results
    ///     8. Return response
    ///
    /// This service acts as the orchestration layer and should
    /// contain minimal provider-specific logic.
    /// </summary>
    public interface IRecordQueryEngine<TRecord> : IRecordQueryEngine
            where TRecord : class
    {
        Task<QueryResult<TRecord>> ExecuteTypedAsync(KaleidoQueryRequest request, CancellationToken cancellationToken = default);
    }
    public interface IRecordQueryEngine
    {
        Task<IRecordQueryResult> ExecuteAsync(KaleidoQueryRequest request, CancellationToken cancellationToken = default);
    }
}
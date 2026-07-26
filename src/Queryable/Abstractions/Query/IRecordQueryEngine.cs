namespace Kaleido.Queryable.Query
{
    internal interface IRecordQueryEngine<TRecord>
            where TRecord : class
    {
        Task<QueryResult<TRecord>> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default);
    }
}
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable
{
    public interface IQueryableCatalog
    {
        IReadOnlyCollection<RecordMetadata> GetRecordDescriptors();

        Task<QueryResponse<TRecord>> QueryAsync<TRecord>(string recordKey, QueryRequest request, CancellationToken cancellationToken = default) where TRecord : class;
    }
}
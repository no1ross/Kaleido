using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;

namespace Kaleido.Queryable;

public sealed class QueryableCatalog : IQueryableCatalog
{
    private readonly IRecordRegistry _registry;
    private readonly IRecordDispatcher _dispatcher;

    public QueryableCatalog(IRecordRegistry registry, IRecordDispatcher dispatcher)
    {
        _registry = registry;
        _dispatcher = dispatcher;
    }

    public IReadOnlyCollection<RecordMetadata> GetRecordDescriptors()
    {
        return _registry.Registrations.Select(x => x.Metadata).ToArray();
    }

    public async Task<QueryResponse<TRecord>> QueryAsync<TRecord>(string recordKey, QueryRequest request, CancellationToken cancellationToken = default) where TRecord : class
    {
        return await _dispatcher.DispatchAsync<TRecord>(recordKey, request, cancellationToken);
    }
}

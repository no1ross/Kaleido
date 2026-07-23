using System.Collections.Concurrent;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable;

public sealed class RecordMetadataCatalog : IRecordMetadataCatalog
{
    private readonly ConcurrentDictionary<Type, RuntimeRecordMetadata> _cache = new();
    public RuntimeRecordMetadata GetMetadata<TRecord>() where TRecord : class => GetMetadata(typeof(TRecord));
    public RuntimeRecordMetadata GetMetadata(Type recordType) => _cache.GetOrAdd(recordType, RecordMetadataBuilder.Build);
}

//using Kaleido.Queryable.Attributes;
//using Kaleido.Queryable.Metadata;
//using System.Collections.Concurrent;
//using System.Reflection;

//namespace Kaleido.Queryable;

//public sealed class RecordMetadataCatalog<TRecord> : IRecordMetadataCatalog<TRecord> where TRecord : class
//{
//    private readonly ConcurrentDictionary<Type, RecordMetadata> _cache = new();
//    public RecordMetadata GetMetadata()
//    {
//        return _cache.GetOrAdd(typeof(TRecord), Build);
//    }

//    private static RecordMetadata Build(Type recordType)
//    {
//        var record = recordType.GetCustomAttribute<KaleidoRecordAttribute>()
//            ?? throw new InvalidOperationException($"{recordType.Name} is missing KaleidoRecordAttribute.");
//        var pageable = recordType.GetCustomAttribute<PageableAttribute>();
//        var fields = recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(BuildField).ToArray();


//        return new RecordMetadata(
//            record.Name, 
//            record.Description, 
//            record.Version, 
//            record.Source, 
//            fields,
//            null, // NamedQueries are not implemented in this example
//            pageable is null ? null : new PageableMetadata(pageable.DefaultSize, pageable.MaxSize));
//    }

//    private static FieldMetadata BuildField(PropertyInfo property)
//    {
//        var filterable = property.GetCustomAttribute<FilterableAttribute>();
//        var searchable = property.GetCustomAttribute<SearchableAttribute>();
//        var sortable = property.GetCustomAttribute<SortableAttribute>();

//        return new FieldMetadata(
//            property.Name,
//            property.PropertyType,
//            filterable is not null,
//            filterable?.Operators ?? Array.Empty<FilterOperator>(),
//            searchable is not null,
//            searchable?.Priority,
//            searchable?.MatchModes ?? Array.Empty<MatchMode>(),
//            sortable is not null);
//    }
//}

//using System.Reflection;
//using Kaleido.Queryable.Attributes;

//namespace Kaleido.Queryable.Metadata;

//public static class RecordMetadataBuilder
//{
//    public static RecordMetadata Build<TRecord>() where TRecord : class => Build(typeof(TRecord));

//    public static RecordMetadata Build(Type recordType)
//    {
//        var record = recordType.GetCustomAttribute<KaleidoRecordAttribute>()
//            ?? throw new InvalidOperationException($"{recordType.Name} is missing KaleidoRecordAttribute.");
//        var pageable = recordType.GetCustomAttribute<PageableAttribute>();
//        var fields = recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(BuildField).ToArray();

//        return new RecordMetadata(record.Name, record.Description, record.Version, record.Source, fields, allowed,
//            pageable is null ? null : new PageableMetadata(pageable.DefaultSize, pageable.MaxSize));
//    }


//}

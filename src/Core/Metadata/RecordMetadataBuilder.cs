using System.Reflection;
using Kaleido.Attributes;

namespace Kaleido.Metadata;

public static class RecordMetadataBuilder
{
    public static RuntimeRecordMetadata Build<TRecord>() where TRecord : class => Build(typeof(TRecord));

    public static RuntimeRecordMetadata Build(Type recordType)
    {
        var record = recordType.GetCustomAttribute<KaleidoRecordAttribute>()
            ?? throw new InvalidOperationException($"{recordType.Name} is missing KaleidoRecordAttribute.");
        var pageable = recordType.GetCustomAttribute<PageableAttribute>();
        var fields = recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(BuildField).ToArray();
        var allowed = recordType.GetCustomAttributes<AllowedQueryAttribute>()
            .Select(x => new RuntimeAllowedQueryMetadata(x.Name, x.Description, x.Parameters)).ToArray();

        return new RuntimeRecordMetadata(record.Name, record.Version, record.Source, fields, allowed,
            pageable is null ? null : new RuntimePageableMetadata(pageable.DefaultSize, pageable.MaxSize));
    }

    private static RuntimeFieldMetadata BuildField(PropertyInfo property)
    {
        var filterable = property.GetCustomAttribute<FilterableAttribute>();
        var searchable = property.GetCustomAttribute<SearchableAttribute>();
        var sortable = property.GetCustomAttribute<SortableAttribute>();

        return new RuntimeFieldMetadata(
            property.Name,
            property.PropertyType,
            filterable is not null,
            filterable?.Operators ?? Array.Empty<FilterOperator>(),
            searchable is not null,
            searchable?.Priority,
            searchable?.MatchModes ?? Array.Empty<MatchMode>(),
            sortable is not null);
    }
}

using Kaleido;
using Kaleido.Extensions;
using Kaleido.Metadata;

public sealed class RecordDescriptorFactory
    : IRecordDescriptorFactory
{
    private static readonly IReadOnlyDictionary<Type, DataTypeDescriptor>
        TypeMappings =
            new Dictionary<Type, DataTypeDescriptor>
            {
                [typeof(string)] = new("string"),

                [typeof(bool)] = new("boolean"),

                [typeof(byte)] = new("integer"),
                [typeof(sbyte)] = new("integer"),
                [typeof(short)] = new("integer"),
                [typeof(ushort)] = new("integer"),
                [typeof(int)] = new("integer"),
                [typeof(uint)] = new("integer"),

                [typeof(long)] = new("integer", "int64"),
                [typeof(ulong)] = new("integer", "int64"),

                [typeof(float)] = new("number", "float"),
                [typeof(double)] = new("number", "double"),
                [typeof(decimal)] = new("number", "decimal"),

                [typeof(Guid)] = new("string", "uuid"),

                [typeof(DateOnly)] = new("string", "date"),

                [typeof(TimeOnly)] = new("string", "time"),

                [typeof(DateTime)] = new("string", "date-time"),

                [typeof(DateTimeOffset)] = new("string", "date-time-offset"),

                [typeof(TimeSpan)] = new("string", "duration")
            };

    public RecordDescriptor Create(
        RuntimeRecordMetadata metadata)
    {
        return new RecordDescriptor(
            metadata.Name,
            metadata.Version,
            metadata.Source,

            metadata.Fields
                .Select(CreateField)
                .ToArray(),

            metadata.AllowedQueries
                .Select(x => new AllowedQueryDescriptor(
                    x.Name,
                    x.Description,
                    x.Parameters))
                .ToArray(),

            metadata.Pageable is null
                ? null
                : new PageableDescriptor(
                    metadata.Pageable.DefaultSize,
                    metadata.Pageable.MaxSize));
    }

    private static FieldDescriptor CreateField(
        RuntimeFieldMetadata field)
    {
        return new FieldDescriptor(
            field.Name,

            CreateDataType(field.FieldType),

            field.IsFilterable,

            field.FilterOperators
                .Select(x => x.GetDescription())
                .ToArray(),

            field.IsSearchable,

            field.MatchModes
                .Select(x => x.GetDescription())
                .ToArray(),

            field.IsSortable);
    }

    private static DataTypeDescriptor CreateDataType(
        Type fieldType)
    {
        var type =
            Nullable.GetUnderlyingType(fieldType)
            ?? fieldType;

        if (type.IsEnum)
        {
            return new("string", "enum");
        }

        return TypeMappings.TryGetValue(
            type,
            out var descriptor)
            ? descriptor
            : new("object");
    }
}
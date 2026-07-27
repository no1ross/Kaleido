namespace Kaleido;

public sealed record DataTypeDescriptor(
    string Type,
    string? Format = null,
    bool Nullable = false,
    IReadOnlyCollection<string>? EnumValues = null
);

public static class DataTypeMapper
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

    public static DataTypeDescriptor GetDescriptor(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var nullable =
            Nullable.GetUnderlyingType(type);

        var actualType =
            nullable ?? type;

        var descriptor =
            Lookup(actualType);

        return descriptor with
        {
            Nullable = nullable is not null
        };
    }

    private static DataTypeDescriptor Lookup(Type type)
    {
        if (TypeMappings.TryGetValue(type, out var descriptor))
        {
            return descriptor;
        }

        if (type.IsEnum)
        {
            return new DataTypeDescriptor(
                "string",
                "enum",
                EnumValues: Enum.GetNames(type)
            );
        }

        if (type.IsArray)
        {
            return new DataTypeDescriptor(
                "array");
        }

        if (typeof(System.Collections.IEnumerable)
            .IsAssignableFrom(type)
            && type != typeof(string))
        {
            return new DataTypeDescriptor(
                "array");
        }

        return new DataTypeDescriptor(
            "object");
    }

    public static bool IsSupportedType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var actualType =
            Nullable.GetUnderlyingType(type)
            ?? type;

        if (TypeMappings.ContainsKey(actualType))
        {
            return true;
        }

        if (actualType.IsEnum)
        {
            return true;
        }

        return false;
    }
}

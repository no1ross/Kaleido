using System.ComponentModel;
using System.Reflection;

namespace Kaleido;

public sealed record DataTypeDescriptor(
    string Type,
    string? Format = null,
    bool Nullable = false,
    IReadOnlyCollection<EnumValueDescriptor>? EnumValues = null,
    DataTypeDescriptor? ItemType = null
);
public sealed record EnumValueDescriptor(
    int Value,
    string Name,
    string? Description);

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
            var values =
                Enum.GetValues(type)
                    .Cast<Enum>()
                    .Select(x =>
                    {
                        var member =
                            type.GetMember(x.ToString())[0];

                        var description =
                            member
                                .GetCustomAttribute<DescriptionAttribute>()
                                ?.Description;

                        return new EnumValueDescriptor(
                            Value: Convert.ToInt32(x),
                            Name: x.ToString(),
                            Description: description);
                    })
                    .ToArray();

            return new DataTypeDescriptor(
                "string",
                "enum",
                EnumValues: values);
        }

        if (type.IsArray)
        {
            return new DataTypeDescriptor(
                "array",
                ItemType: GetDescriptor(type.GetElementType()!));
        }

        if (typeof(System.Collections.IEnumerable)
                .IsAssignableFrom(type)
            && type != typeof(string))
        {
            var elementType =
                type.IsGenericType
                    ? type.GetGenericArguments()[0]
                    : typeof(object);

            return new DataTypeDescriptor(
                "array",
                ItemType: GetDescriptor(elementType));
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

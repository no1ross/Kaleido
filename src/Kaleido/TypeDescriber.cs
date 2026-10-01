using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Kaleido;

[ExcludeFromCodeCoverage]
public sealed record DataTypeDescriptor(
    string Type,
    string? Format = null,
    bool Nullable = false,
    IReadOnlyCollection<EnumValueDescriptor>? EnumValues = null,
    DataTypeDescriptor? ItemType = null);

[ExcludeFromCodeCoverage]
public sealed record EnumValueDescriptor(
    int Value,
    string Name,
    string? Description);

public interface ITypeDescriber
{
    DataTypeDescriptor GetDescriptor(PropertyInfo propertyInfo);

    /// <summary>
    /// Returns true if <paramref name="type"/> is a known scalar or enum type
    /// that the framework can describe and that transport layers are expected
    /// to deliver as a typed CLR value.
    /// </summary>
    bool IsSupportedType(Type type);
}

internal sealed class TypeDescriber : ITypeDescriber
{
    private static readonly IReadOnlyDictionary<Type, DataTypeDescriptor> TypeMappings =
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

    public bool IsSupportedType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var actualType = Nullable.GetUnderlyingType(type) ?? type;

        return TypeMappings.ContainsKey(actualType) || actualType.IsEnum;
    }

    public DataTypeDescriptor GetDescriptor(
        PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        var descriptor =
            GetDescriptor(propertyInfo.PropertyType);

        NullabilityInfo nullability;

        // NullabilityInfoContext is reusable but NOT thread-safe — serialize
        // access on the shared instance rather than allocating one per property.
        lock (NullabilityContext)
        {
            nullability = NullabilityContext.Create(propertyInfo);
        }

        return descriptor with
        {
            Nullable = descriptor.Nullable
                || nullability.ReadState == NullabilityState.Nullable
        };
    }

    internal DataTypeDescriptor GetDescriptor(
        Type type)
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

    private static readonly ConcurrentDictionary<Type, DataTypeDescriptor> DescriptorCache = new();

    // Shared context — allocating one per property was pure waste on the
    // metadata-reflection path; NullabilityInfoContext is designed to be reused.
    private static readonly NullabilityInfoContext NullabilityContext = new();

    private DataTypeDescriptor Lookup(
        Type type)
    {
        if (TypeMappings.TryGetValue(
                type,
                out var descriptor))
        {
            return descriptor;
        }

        return DescriptorCache.GetOrAdd(
            type,
            BuildDescriptor);
    }

    private DataTypeDescriptor BuildDescriptor(
        Type type)
    {
        if (type.IsEnum)
        {
            var values =
                Enum.GetValues(type)
                    .Cast<Enum>()
                    .Select(x =>
                    {
                        var members = type.GetMember(x.ToString());
                        var member = members.Length > 0
                            ? members[0]
                            : throw new KaleidoFrameworkException(
                                FrameworkErrorCodes.ReflectionError,
                                $"Enum member '{x}' not found in type '{type.FullName}'.");

                        var description =
                            member
                                .GetCustomAttribute<DescriptionAttribute>()
                                ?.Description;

                        return new EnumValueDescriptor(
                            Value: Convert.ToInt32(x, CultureInfo.InvariantCulture),
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
            var elementType = type.GetElementType()
                ?? throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.ReflectionError,
                    $"Array type '{type.FullName}' has null element type.");

            return new DataTypeDescriptor(
                "array",
                ItemType: GetDescriptor(elementType));
        }

        if (typeof(System.Collections.IEnumerable)
                .IsAssignableFrom(type)
            && type != typeof(string))
        {
            var elementType =
                type.IsGenericType
                    ? type.GetGenericArguments().Length > 0
                        ? type.GetGenericArguments()[0]
                        : throw new KaleidoFrameworkException(
                            FrameworkErrorCodes.ReflectionError,
                            $"Generic type '{type.FullName}' has no generic arguments.")
                    : typeof(object);

            return new DataTypeDescriptor(
                "array",
                ItemType: GetDescriptor(elementType));
        }

        return new DataTypeDescriptor(
            "object");
    }

}
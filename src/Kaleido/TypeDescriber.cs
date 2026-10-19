using System.Collections.Frozen;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Kaleido;

/// <summary>
/// The transport-friendly description of a field's CLR type, published in the registry metadata of
/// Process steps and Queryable sources and views, so clients and AI agents know what value to send
/// or expect.
/// </summary>
/// <remarks>
/// The framework derives descriptors from CLR types at startup. Scalars map to <c>"string"</c>,
/// <c>"boolean"</c>, <c>"integer"</c> or <c>"number"</c>, with a <paramref name="Format"/> where the base type
/// alone is ambiguous; enums map to <c>"string"</c> with format <c>"enum"</c> and their members in
/// <paramref name="EnumValues"/>; arrays and other enumerables (except <see langword="string"/>) map to
/// <c>"array"</c> with an <paramref name="ItemType"/>; every other type maps to <c>"object"</c>.
/// </remarks>
/// <param name="Type">
/// The base type: <c>"string"</c>, <c>"boolean"</c>, <c>"integer"</c>, <c>"number"</c>, <c>"array"</c> or
/// <c>"object"</c>.
/// </param>
/// <param name="Format">
/// Refines <paramref name="Type"/>, or <see langword="null"/> when the base type is enough:
/// <c>"int64"</c> (<see langword="long"/>, <see langword="ulong"/>), <c>"float"</c>, <c>"double"</c>, <c>"decimal"</c>,
/// <c>"uuid"</c> (<see cref="Guid"/>), <c>"date"</c> (<see cref="DateOnly"/>), <c>"time"</c>
/// (<see cref="TimeOnly"/>), <c>"date-time"</c> (<see cref="DateTime"/>), <c>"date-time-offset"</c>
/// (<see cref="DateTimeOffset"/>), <c>"duration"</c> (<see cref="TimeSpan"/>) or <c>"enum"</c>.
/// </param>
/// <param name="Nullable">
/// <see langword="true"/> when the value may be <see langword="null"/>: a <see cref="System.Nullable{T}"/> type,
/// or, for a property, a reference type annotated as nullable.
/// </param>
/// <param name="EnumValues">
/// The members of an enum type (format <c>"enum"</c>); <see langword="null"/> for every other type.
/// </param>
/// <param name="ItemType">
/// The descriptor of the element type of an <c>"array"</c>; <see langword="null"/> for every other type.
/// </param>
[ExcludeFromCodeCoverage]
internal sealed record DataTypeDescriptor(
    string Type,
    string? Format = null,
    bool Nullable = false,
    IReadOnlyCollection<EnumValueDescriptor>? EnumValues = null,
    DataTypeDescriptor? ItemType = null);

/// <summary>
/// One member of an enum type, listed in <see cref="DataTypeDescriptor.EnumValues"/>.
/// </summary>
/// <param name="Value">The member's underlying numeric value, converted to <see cref="int"/>.</param>
/// <param name="Name">The member's name, as declared in code.</param>
/// <param name="Description">
/// The member's <see cref="DescriptionAttribute"/> text, or <see langword="null"/> when it has none.
/// </param>
[ExcludeFromCodeCoverage]
internal sealed record EnumValueDescriptor(
    int Value,
    string Name,
    string? Description);

internal interface ITypeDescriber
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
    private static readonly FrozenDictionary<Type, DataTypeDescriptor> TypeMappings =
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

                [typeof(TimeSpan)] = new("string", "duration"),
            }.ToFrozenDictionary();

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
                || nullability.ReadState == NullabilityState.Nullable,
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
            Nullable = nullable is not null,
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
            return EnumDescriptor(type);
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

    // An enum is a string with its members as values, each with its [Description].
    private static DataTypeDescriptor EnumDescriptor(Type type)
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

}

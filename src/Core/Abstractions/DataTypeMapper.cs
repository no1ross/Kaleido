using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Kaleido;

public sealed record DataTypeDescriptor(
    string Type,
    string? Format = null,
    bool Nullable = false,
    IReadOnlyCollection<EnumValueDescriptor>? EnumValues = null,
    DataTypeDescriptor? ItemType = null);

public sealed record EnumValueDescriptor(
    int Value,
    string Name,
    string? Description);

public sealed record DataTypeConversionResult
{
    public bool Success { get; init; }

    public object? Value { get; init; }

    public string? ErrorMessage { get; init; }

    public static DataTypeConversionResult Successful(
        object? value)
    {
        return new()
        {
            Success = true,
            Value = value
        };
    }

    public static DataTypeConversionResult Failed(
        string errorMessage)
    {
        return new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

public sealed record DataTypeConversionResult<TValue>
{
    public bool Success { get; init; }

    public TValue? Value { get; init; }

    public string? ErrorMessage { get; init; }

    public static DataTypeConversionResult<TValue> Successful(
        TValue? value)
    {
        return new()
        {
            Success = true,
            Value = value
        };
    }

    public static DataTypeConversionResult<TValue> Failed(
        string errorMessage)
    {
        return new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

public sealed class UnsupportedDataTypeException : Exception
{
    public UnsupportedDataTypeException(
        Type dataType)
        : base(
            $"Type '{dataType.FullName}' is not supported by DataTypeMapper.")
    {
        DataType = dataType;
    }
    public Type DataType { get; }
}

public sealed class DataTypeConversionException : Exception
{
    public DataTypeConversionException(
        object? value,
        Type targetType,
        string message)
        : base(message)
    {
        Value = value;
        TargetType = targetType;
    }

    public object? Value { get; }

    public Type TargetType { get; }
}

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

    public static DataTypeDescriptor GetDescriptor(
        PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        var descriptor =
            GetDescriptor(propertyInfo.PropertyType);

        var nullability =
            new NullabilityInfoContext()
                .Create(propertyInfo);

        return descriptor with
        {
            Nullable = descriptor.Nullable
                || nullability.ReadState == NullabilityState.Nullable
        };
    }

    internal static DataTypeDescriptor GetDescriptor(
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

    public static bool IsSupportedType(
        Type type)
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

    public static DataTypeConversionResult TryConvertValue(
        object? value,
        Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        var actualType =
            Nullable.GetUnderlyingType(targetType)
            ?? targetType;

        if (value is null)
        {
            if (!CanAcceptNull(targetType))
            {
                return DataTypeConversionResult.Failed(
                    $"Null is not a valid value for non-nullable type '{actualType.Name}'.");
            }

            return DataTypeConversionResult.Successful(null);
        }

        if (!IsSupportedType(actualType))
        {
            throw new UnsupportedDataTypeException(
                actualType);
        }

        if (actualType.IsInstanceOfType(value))
        {
            return DataTypeConversionResult.Successful(value);
        }

        if (value is JsonElement jsonElement)
        {
            value =
                ConvertJsonElement(
                    jsonElement);
        }

        if (value is null)
        {
            if (!CanAcceptNull(targetType))
            {
                return DataTypeConversionResult.Failed(
                    $"Null is not a valid value for non-nullable type '{actualType.Name}'.");
            }

            return DataTypeConversionResult.Successful(null);
        }

        if (actualType.IsInstanceOfType(value))
        {
            return DataTypeConversionResult.Successful(value);
        }

        var stringValue =
            Convert.ToString(
                value,
                CultureInfo.InvariantCulture);

        if (stringValue is null)
        {
            return DataTypeConversionResult.Failed(
                $"Unable to convert value to type '{actualType.Name}'.");
        }

        if (actualType == typeof(string))
        {
            return DataTypeConversionResult.Successful(
                stringValue);
        }

        if (actualType == typeof(bool))
        {
            return bool.TryParse(
                stringValue,
                out var boolValue)
                ? DataTypeConversionResult.Successful(boolValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(byte))
        {
            return byte.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var byteValue)
                ? DataTypeConversionResult.Successful(byteValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(sbyte))
        {
            return sbyte.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sbyteValue)
                ? DataTypeConversionResult.Successful(sbyteValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(short))
        {
            return short.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var shortValue)
                ? DataTypeConversionResult.Successful(shortValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(ushort))
        {
            return ushort.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var ushortValue)
                ? DataTypeConversionResult.Successful(ushortValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(int))
        {
            return int.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var intValue)
                ? DataTypeConversionResult.Successful(intValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(uint))
        {
            return uint.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var uintValue)
                ? DataTypeConversionResult.Successful(uintValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(long))
        {
            return long.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var longValue)
                ? DataTypeConversionResult.Successful(longValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(ulong))
        {
            return ulong.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var ulongValue)
                ? DataTypeConversionResult.Successful(ulongValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(float))
        {
            return float.TryParse(
                stringValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var floatValue)
                ? DataTypeConversionResult.Successful(floatValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(double))
        {
            return double.TryParse(
                stringValue,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var doubleValue)
                ? DataTypeConversionResult.Successful(doubleValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(decimal))
        {
            return decimal.TryParse(
                stringValue,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var decimalValue)
                ? DataTypeConversionResult.Successful(decimalValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(Guid))
        {
            return Guid.TryParse(
                stringValue,
                out var guidValue)
                ? DataTypeConversionResult.Successful(guidValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(DateOnly))
        {
            return DateOnly.TryParse(
                stringValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnlyValue)
                ? DataTypeConversionResult.Successful(dateOnlyValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(TimeOnly))
        {
            return TimeOnly.TryParse(
                stringValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timeOnlyValue)
                ? DataTypeConversionResult.Successful(timeOnlyValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(DateTime))
        {
            return DateTime.TryParse(
                stringValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dateTimeValue)
                ? DataTypeConversionResult.Successful(dateTimeValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.TryParse(
                stringValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dateTimeOffsetValue)
                ? DataTypeConversionResult.Successful(dateTimeOffsetValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType == typeof(TimeSpan))
        {
            return TimeSpan.TryParse(
                stringValue,
                CultureInfo.InvariantCulture,
                out var timeSpanValue)
                ? DataTypeConversionResult.Successful(timeSpanValue)
                : FailedValue(
                    stringValue,
                    actualType);
        }

        if (actualType.IsEnum)
        {
            return TryConvertEnum(
                stringValue,
                actualType);
        }

        throw new UnsupportedDataTypeException(
            actualType);
    }

    public static DataTypeConversionResult<TValue> TryConvertValue<TValue>(
        object? value)
    {
        var result =
            TryConvertValue(
                value,
                typeof(TValue));

        if (!result.Success)
        {
            return DataTypeConversionResult<TValue>.Failed(
                result.ErrorMessage ?? "Unable to convert value.");
        }

        return DataTypeConversionResult<TValue>.Successful(
            (TValue?)result.Value);
    }

    public static object? ConvertValue(
        object? value,
        Type targetType)
    {
        var result =
            TryConvertValue(
                value,
                targetType);

        if (!result.Success)
        {
            throw new DataTypeConversionException(
                value,
                targetType,
                result.ErrorMessage ?? "Unable to convert value.");
        }

        return result.Value;
    }

    public static TValue? ConvertValue<TValue>(
        object? value)
    {
        var result =
            TryConvertValue<TValue>(
                value);

        if (!result.Success)
        {
            throw new DataTypeConversionException(
                value,
                typeof(TValue),
                result.ErrorMessage ?? "Unable to convert value.");
        }

        return result.Value;
    }

    private static DataTypeDescriptor Lookup(
        Type type)
    {
        if (TypeMappings.TryGetValue(
                type,
                out var descriptor))
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

    private static DataTypeConversionResult TryConvertEnum(
        string stringValue,
        Type enumType)
    {
        if (!Enum.TryParse(
                enumType,
                stringValue,
                true,
                out var enumValue)
            || enumValue is null)
        {
            return FailedValue(
                stringValue,
                enumType);
        }

        if (!Enum.IsDefined(
                enumType,
                enumValue))
        {
            return FailedValue(
                stringValue,
                enumType);
        }

        return DataTypeConversionResult.Successful(
            enumValue);
    }

    private static DataTypeConversionResult FailedValue(
        string value,
        Type targetType)
    {
        return DataTypeConversionResult.Failed(
            $"'{value}' is not a valid value for type '{targetType.Name}'.");
    }

    private static bool CanAcceptNull(
        Type type)
    {
        if (!type.IsValueType)
        {
            return true;
        }

        return Nullable.GetUnderlyingType(type) is not null;
    }

    private static object? ConvertJsonElement(
        JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String =>
                element.GetString(),

            JsonValueKind.Number =>
                element.GetRawText(),

            JsonValueKind.True =>
                true,

            JsonValueKind.False =>
                false,

            JsonValueKind.Null =>
                null,

            JsonValueKind.Undefined =>
                null,

            _ =>
                element.GetRawText()
        };
    }
}
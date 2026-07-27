using System.Text.Json;

namespace Kaleido.Json;

public static class ValueConverter
{
    public static object? Convert(
        object? value,
        Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (value is null)
        {
            return null;
        }

        var actualType =
            Nullable.GetUnderlyingType(targetType)
            ?? targetType;

        if (value is JsonElement json)
        {
            return ConvertJsonElement(
                json,
                actualType);
        }

        if (actualType.IsEnum)
        {
            return Enum.Parse(
                actualType,
                value.ToString()!,
                ignoreCase: true);
        }

        if (actualType == typeof(Guid))
        {
            return value is Guid guid
                ? guid
                : Guid.Parse(value.ToString()!);
        }

        if (actualType == typeof(DateOnly))
        {
            return value is DateOnly dateOnly
                ? dateOnly
                : DateOnly.Parse(value.ToString()!);
        }

        if (actualType == typeof(TimeOnly))
        {
            return value is TimeOnly timeOnly
                ? timeOnly
                : TimeOnly.Parse(value.ToString()!);
        }

        if (actualType == typeof(DateTime))
        {
            return value is DateTime dateTime
                ? dateTime
                : DateTime.Parse(value.ToString()!);
        }

        if (actualType == typeof(DateTimeOffset))
        {
            return value is DateTimeOffset dateTimeOffset
                ? dateTimeOffset
                : DateTimeOffset.Parse(value.ToString()!);
        }

        if (actualType == typeof(TimeSpan))
        {
            return value is TimeSpan timeSpan
                ? timeSpan
                : TimeSpan.Parse(value.ToString()!);
        }

        return System.Convert.ChangeType(
            value,
            actualType);
    }

    private static object? ConvertJsonElement(
        JsonElement element,
        Type targetType)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (targetType == typeof(string))
        {
            return element.GetString();
        }

        if (targetType == typeof(bool))
        {
            return element.GetBoolean();
        }

        if (targetType == typeof(byte))
        {
            return element.GetByte();
        }

        if (targetType == typeof(sbyte))
        {
            return (sbyte)element.GetInt32();
        }

        if (targetType == typeof(short))
        {
            return element.GetInt16();
        }

        if (targetType == typeof(ushort))
        {
            return element.GetUInt16();
        }

        if (targetType == typeof(int))
        {
            return element.GetInt32();
        }

        if (targetType == typeof(uint))
        {
            return element.GetUInt32();
        }

        if (targetType == typeof(long))
        {
            return element.GetInt64();
        }

        if (targetType == typeof(ulong))
        {
            return element.GetUInt64();
        }

        if (targetType == typeof(float))
        {
            return element.GetSingle();
        }

        if (targetType == typeof(double))
        {
            return element.GetDouble();
        }

        if (targetType == typeof(decimal))
        {
            return element.GetDecimal();
        }

        if (targetType == typeof(Guid))
        {
            return element.GetGuid();
        }

        if (targetType == typeof(DateTime))
        {
            return element.GetDateTime();
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return element.GetDateTimeOffset();
        }

        if (targetType == typeof(DateOnly))
        {
            return DateOnly.Parse(
                element.GetString()!);
        }

        if (targetType == typeof(TimeOnly))
        {
            return TimeOnly.Parse(
                element.GetString()!);
        }

        if (targetType == typeof(TimeSpan))
        {
            return TimeSpan.Parse(
                element.GetString()!);
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(
                targetType,
                element.GetString()!,
                ignoreCase: true);
        }

        return JsonSerializer.Deserialize(
            element.GetRawText(),
            targetType);
    }
}
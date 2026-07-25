using System.Text.Json;

namespace Kaleido.Queryable.Query;

public static class QueryValueConverter
{
    public static object? Normalize(
        object? value)
    {
        if (value is not JsonElement json)
        {
            return value;
        }

        return json.ValueKind switch
        {
            JsonValueKind.Null => null,

            JsonValueKind.String =>
                json.GetString(),

            JsonValueKind.True =>
                true,

            JsonValueKind.False =>
                false,

            JsonValueKind.Number =>
                NormalizeNumber(json),

            _ =>
                json.ToString()
        };
    }

    public static object? ConvertTo(object? value, Type targetType)
    {
        value = Normalize(value);

        if (value is null)
        {
            return null;
        }

        targetType =
            Nullable.GetUnderlyingType(targetType)
            ?? targetType;

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value.ToString()!, ignoreCase: true);
        }

        if (targetType == typeof(DateOnly))
        {
            return DateOnly.Parse(value.ToString()!);
        }

        if (targetType == typeof(TimeOnly))
        {
            return TimeOnly.Parse(value.ToString()!);
        }

        if (targetType == typeof(Guid))
        {
            return Guid.Parse(value.ToString()!);
        }

        return QueryValueConverter.ConvertTo(value, targetType);
    }

    private static object NormalizeNumber(JsonElement json)
    {
        if (json.TryGetInt32(out var int32))
        {
            return int32;
        }

        if (json.TryGetInt64(out var int64))
        {
            return int64;
        }

        if (json.TryGetDecimal(out var dec))
        {
            return dec;
        }

        return json.GetDouble();
    }
}
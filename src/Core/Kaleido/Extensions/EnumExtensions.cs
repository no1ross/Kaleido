using System.ComponentModel;
using System.Reflection;

namespace Kaleido.Extensions;

public static class EnumExtensions
{
    public static string GetDescription(
        this Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return GetDescriptionAttribute(value)?.Description
            ?? value.ToString();
    }

    public static bool TryParseFromDescription<TEnum>(
        string? value,
        out TEnum result)
        where TEnum : struct, Enum
    {
        var success = TryParseFromDescription(
            typeof(TEnum),
            value,
            out var parsed);

        result = parsed is TEnum typed
            ? typed
            : default;

        return success;
    }

    public static bool TryParseFromDescription(
        Type enumType,
        string? value,
        out object? result)
    {
        result = null;

        if (!enumType.IsEnum ||
            string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        foreach (var item in Enum.GetValues(enumType))
        {
            var enumValue = (Enum)item;

            if (string.Equals(
                    enumValue.GetDescription(),
                    text,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    enumValue.ToString(),
                    text,
                    StringComparison.OrdinalIgnoreCase))
            {
                result = item;
                return true;
            }
        }

        return false;
    }

    private static DescriptionAttribute? GetDescriptionAttribute(
        Enum value)
    {
        return value
            .GetType()
            .GetMember(value.ToString())
            .FirstOrDefault()?
            .GetCustomAttribute<DescriptionAttribute>();
    }
}
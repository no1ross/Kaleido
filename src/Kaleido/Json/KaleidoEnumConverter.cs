using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Json;

public sealed class KaleidoEnumConverter<TEnum>
    : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value =
            reader.GetString();

        if (Enum.TryParse<TEnum>(
                value,
                ignoreCase: true,
                out var result))
        {
            return result;
        }

        throw new JsonException(
            $"Invalid {typeof(TEnum).Name} value '{value}'.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        TEnum value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(
            value.ToString());
    }
}
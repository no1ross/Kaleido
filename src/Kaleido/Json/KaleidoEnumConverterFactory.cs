using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Json;

public sealed class KaleidoEnumConverterFactory
    : JsonConverterFactory
{
    public override bool CanConvert(
        Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var converterType =
            typeof(KaleidoEnumConverter<>)
                .MakeGenericType(typeToConvert);

        return (JsonConverter)Activator
            .CreateInstance(converterType)!;
    }
}

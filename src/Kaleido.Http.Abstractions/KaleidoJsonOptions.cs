using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Http;

/// <summary>
/// The canonical <see cref="JsonSerializerOptions"/> used by all Kaleido HTTP
/// transport code — server response serialization and client-side read/write.
/// Ensures a consistent wire format everywhere.
///
/// Foreign-language consumers (Java, Python, etc.) do not need these options —
/// they interact purely via the JSON wire contract documented in README.md.
/// </summary>
public static class KaleidoJsonOptions
{
    /// <summary>
    /// Shared options: camelCase property names, case-insensitive binding,
    /// enums serialized as camelCase strings (no integer values).
    /// </summary>
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));

        return options;
    }
}

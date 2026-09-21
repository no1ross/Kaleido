using System.Net.Http.Json;
using System.Text.Json;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Infrastructure;

internal static class ProcessHttpJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static Task<T?> ReadAsync<T>(
        this HttpContent content,
        CancellationToken cancellationToken = default) =>
        content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);

    public static JsonElement EmptyObject() =>
        JsonSerializer.SerializeToElement(new { }, SerializerOptions);
}

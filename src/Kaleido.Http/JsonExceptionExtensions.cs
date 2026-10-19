using System.Text.Json;

namespace Kaleido.Http;

internal static class JsonExceptionExtensions
{
    // The serializer's message without its line/position suffix, led by the JSON path that failed.
    public static string Describe(this JsonException exception)
    {
        var message = exception.Message.Split(" Path:", 2)[0].Trim();

        return exception.Path is { Length: > 0 } path
            ? $"The value at '{path}' is not valid: {message}"
            : message;
    }
}

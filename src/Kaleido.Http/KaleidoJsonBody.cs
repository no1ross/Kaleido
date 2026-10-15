using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http;

/// <summary>
/// A Kaleido endpoint's request body that the endpoint only ever reads as JSON as sent: the
/// idempotency filter hashes it without knowing its type.
/// </summary>
internal interface IKaleidoJsonBody
{
    /// <summary>The body as sent, or empty when it was not valid JSON.</summary>
    string RawJson { get; }
}

/// <summary>
/// A Kaleido endpoint's JSON request body, deserialized as <typeparamref name="T"/> without ever
/// failing the binding.
/// </summary>
/// <remarks>
/// Minimal-API body binding fails before endpoint filters run, so a body that does not fit its type
/// would reach the host's exception handling (in Development the developer exception page logs an
/// Error) and be answered without a <see cref="KaleidoErrorResponse"/>. Here the failure is kept, and
/// <see cref="Value"/> raises it inside the endpoint, where <see cref="KaleidoExceptionEndpointFilter"/>
/// answers 400 (415 for a body that is not JSON) naming the JSON path.
/// </remarks>
/// <typeparam name="T">The typed request the endpoint takes.</typeparam>
internal sealed class KaleidoJsonBody<T> : IKaleidoJsonBody
    where T : class
{
    private readonly T? _value;
    private readonly int _failureStatus;
    private readonly string? _failure;

    private KaleidoJsonBody(string rawJson, T? value, int failureStatus = 0, string? failure = null)
    {
        RawJson = rawJson;
        _value = value;
        _failureStatus = failureStatus;
        _failure = failure;
    }

    /// <inheritdoc />
    public string RawJson { get; }

    /// <summary>The typed request, or a <see cref="BadHttpRequestException"/> naming what does not fit.</summary>
    public T Value =>
        _value ?? throw new BadHttpRequestException(_failure ?? "The request body is empty.", _failure is null ? StatusCodes.Status400BadRequest : _failureStatus);

    /// <summary>Minimal-API binding hook: reads and deserializes the body; never throws for a caller's mistake.</summary>
    public static async ValueTask<KaleidoJsonBody<T>?> BindAsync(HttpContext context)
    {
        if (!context.Request.HasJsonContentType())
        {
            return Failed(string.Empty, StatusCodes.Status415UnsupportedMediaType, "Send the request body as JSON (Content-Type: application/json).");
        }

        JsonElement json;

        try
        {
            json = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body, KaleidoJsonOptions.Options, context.RequestAborted);
        }
        catch (JsonException exception)
        {
            return Failed(string.Empty, StatusCodes.Status400BadRequest, $"The request body is not valid JSON: {Describe(exception)}");
        }

        var raw = json.GetRawText();

        try
        {
            return new KaleidoJsonBody<T>(raw, json.Deserialize<T>(KaleidoJsonOptions.Options));
        }
        catch (JsonException exception)
        {
            return Failed(raw, StatusCodes.Status400BadRequest, Describe(exception));
        }
    }

    private static KaleidoJsonBody<T> Failed(string raw, int status, string failure) =>
        new(raw, value: null, status, failure);

    // The serializer's message without its line/position suffix, led by the JSON path that failed.
    private static string Describe(JsonException exception)
    {
        var message = exception.Message.Split(" Path:", 2)[0].Trim();

        return exception.Path is { Length: > 0 } path
            ? $"The value at '{path}' is not valid: {message}"
            : message;
    }
}

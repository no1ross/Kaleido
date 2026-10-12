using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kaleido.Processor;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.Processor;

/// <summary>
/// Makes process execution idempotent for a request sent with an <c>Idempotency-Key</c> header: the
/// first successful answer is stored, and a retry with the same key and the same request gets that
/// answer again (with <c>Idempotent-Replayed: true</c>) instead of running the step a second time.
/// </summary>
/// <remarks>
/// The key is scoped per service and caller. The same key with a different request is refused with
/// 422 <c>pro_idempotency_key_reused</c>. Only successful (2xx) answers are stored; a failed request
/// may be retried with the same key. Two requests with the same key at the same time both run; the
/// process store's optimistic concurrency lets only one of them save (the other gets 409). Without the
/// header nothing changes. ASP.NET creates the filter per request, so a scoped store (e.g. the SQLite store) is fine.
/// </remarks>
internal sealed class KaleidoIdempotencyEndpointFilter(
    IKaleidoContextStore store,
    KaleidoHttpOptions httpOptions,
    KaleidoServiceOptions serviceOptions,
    IKaleidoCorrelationContextAccessor correlationAccessor)
    : IEndpointFilter
{
    private const int MaxKeyLength = 200;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[KaleidoIdempotencyHeaders.IdempotencyKey].ToString();

        if (string.IsNullOrWhiteSpace(key))
        {
            return await next(context);
        }

        if (key.Length > MaxKeyLength)
        {
            return Error(StatusCodes.Status400BadRequest, ProcessorErrorCodes.InvalidIdempotencyKey, $"The {KaleidoIdempotencyHeaders.IdempotencyKey} header is longer than {MaxKeyLength} characters.");
        }

        var scopedKey = Scope(key);
        var requestHash = Hash(context);

        if (await store.FindResultAsync(scopedKey, http.RequestAborted) is { } stored)
        {
            return Replay(http, stored, requestHash);
        }

        var result = await next(context);

        if (result is IValueHttpResult valueResult && IsSuccess(result))
        {
            await store.SaveResultAsync(
                new IdempotentResult
                {
                    Key = scopedKey,
                    RequestHash = requestHash,
                    StatusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK,
                    Response = JsonSerializer.Serialize(valueResult.Value, KaleidoJsonOptions.Options),
                    ExpiresUtc = DateTimeOffset.UtcNow + httpOptions.IdempotencyRetention,
                },
                http.RequestAborted);
        }

        return result;
    }

    // Scoped per service and caller: two callers cannot see each other's answers.
    private string Scope(string key) =>
        $"{serviceOptions.ServiceName}:{correlationAccessor.Current.CallerName ?? "anonymous"}:{key}";

    // The request the key was used with: the route, the process it continues, and the bound body.
    private static string Hash(EndpointFilterInvocationContext context)
    {
        var request = context.HttpContext.Request;
        var body = context.Arguments.Count > 0 ? JsonSerializer.Serialize(context.Arguments[0], KaleidoJsonOptions.Options) : string.Empty;
        var payload = $"{request.Method} {request.Path}\n{request.Headers[KaleidoCorrelationHeaders.ProcessId]}\n{body}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static IResult Replay(HttpContext http, IdempotentResult stored, string requestHash)
    {
        if (!string.Equals(stored.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Error(
                StatusCodes.Status422UnprocessableEntity,
                ProcessorErrorCodes.IdempotencyKeyReused,
                $"The {KaleidoIdempotencyHeaders.IdempotencyKey} was already used with a different request. Use a new key for a new call.");
        }

        http.Response.Headers[KaleidoIdempotencyHeaders.Replayed] = "true";

        return Results.Text(stored.Response, "application/json", Encoding.UTF8, stored.StatusCode);
    }

    private static bool IsSuccess(object? result) =>
        (result as IStatusCodeHttpResult)?.StatusCode is null or (>= 200 and < 300);

    private static IResult Error(int statusCode, string code, string message) =>
        Results.Json(new KaleidoErrorResponse([new KaleidoError(code, message)]), KaleidoJsonOptions.Options, statusCode: statusCode);
}
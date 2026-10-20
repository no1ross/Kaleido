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
/// key is admitted in the store before the step runs, the first successful answer is stored, and a
/// retry with the same key and the same request gets that answer again (with
/// <c>Idempotent-Replayed: true</c>) instead of running the step a second time (#285, ADR 0019).
/// </summary>
/// <remarks>
/// <para>
/// The key is scoped per service and caller (the caller's stable subject id, else its name). Admission
/// is atomic in the store, so a request with a key that is still running does not run: 409
/// <c>pro_idempotency_in_progress</c> with <c>Retry-After</c>. A key whose request never finished
/// within <see cref="KaleidoHttpOptions.IdempotencyLease"/> is answered 409
/// <c>pro_idempotency_outcome_unknown</c> and never run again. The same key with a different request
/// is refused with 422 <c>pro_idempotency_key_reused</c>.
/// </para>
/// <para>
/// Only successful (2xx) answers are stored; any other outcome releases the key, so a failed request
/// may be retried with the same key. Both are written with <see cref="CancellationToken.None"/>: a
/// request whose caller has gone (a client timeout) still stores its answer for the retry. Without
/// the header nothing changes. ASP.NET creates the filter per request, so a scoped store (e.g. the
/// SQLite store) is fine.
/// </para>
/// </remarks>
internal sealed class KaleidoIdempotencyEndpointFilter(
    IKaleidoIdempotencyStore store,
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
        var now = DateTimeOffset.UtcNow;

        var existing = await store.AdmitAsync(
            new IdempotentResult
            {
                Key = scopedKey,
                RequestHash = requestHash,
                State = IdempotentResultState.InProgress,
                LeaseUntilUtc = now + httpOptions.IdempotencyLease,
                ExpiresUtc = now + Max(httpOptions.IdempotencyRetention, httpOptions.IdempotencyLease),
            },
            http.RequestAborted);

        if (existing is not null)
        {
            return Answer(http, existing, requestHash);
        }

        object? result = null;

        try
        {
            result = await next(context);
        }
        finally
        {
            await CompleteOrReleaseAsync(scopedKey, requestHash, result);
        }

        return result;
    }

    // The bookkeeping of an admitted key always finishes, even when the caller has gone: a successful
    // answer is stored for the retry; anything else (a failure, an exception) frees the key.
    private async Task CompleteOrReleaseAsync(string scopedKey, string requestHash, object? result)
    {
        if (result is IValueHttpResult valueResult && IsSuccess(result))
        {
            await store.CompleteAsync(
                new IdempotentResult
                {
                    Key = scopedKey,
                    RequestHash = requestHash,
                    State = IdempotentResultState.Completed,
                    StatusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK,
                    Response = JsonSerializer.Serialize(valueResult.Value, KaleidoJsonOptions.Options),
                    ExpiresUtc = DateTimeOffset.UtcNow + httpOptions.IdempotencyRetention,
                },
                CancellationToken.None);

            return;
        }

        await store.ReleaseAsync(scopedKey, CancellationToken.None);
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second) => first > second ? first : second;

    // Scoped per service and caller: two callers cannot see each other's answers. The caller is its
    // stable subject id when the principal has one (a renamed user keeps their keys; two users with the
    // same display name stay apart), else its name; the prefix keeps an id and a name from colliding.
    private string Scope(string key) =>
        $"{serviceOptions.ServiceName}:{Caller(correlationAccessor.Current)}:{key}";

    private static string Caller(KaleidoCorrelationContext caller) =>
        caller.CallerId is { } id ? $"id:{id}"
        : caller.CallerName is { } name ? $"name:{name}"
        : "anonymous";

    // The request the key was used with: the route, the process it continues, and the bound body.
    private static string Hash(EndpointFilterInvocationContext context)
    {
        var request = context.HttpContext.Request;
        var body = context.Arguments.Count > 0 ? context.Arguments[0] switch
        {
            IKaleidoJsonBody json => json.RawJson,
            var argument => JsonSerializer.Serialize(argument, KaleidoJsonOptions.Options),
        } : string.Empty;
        var payload = $"{request.Method} {request.Path}\n{request.Headers[KaleidoCorrelationHeaders.ProcessId]}\n{body}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    // The answer for a key another request holds: a different request is refused; a running one is
    // retried later; one that never finished is not run again; a completed one is replayed.
    private static IResult Answer(HttpContext http, IdempotentResult existing, string requestHash)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Error(
                StatusCodes.Status422UnprocessableEntity,
                ProcessorErrorCodes.IdempotencyKeyReused,
                $"The {KaleidoIdempotencyHeaders.IdempotencyKey} was already used with a different request. Use a new key for a new call.");
        }

        if (existing.State == IdempotentResultState.InProgress)
        {
            if (existing.LeaseUntilUtc > DateTimeOffset.UtcNow)
            {
                http.Response.Headers.RetryAfter = RetryAfterSeconds;

                return Error(
                    StatusCodes.Status409Conflict,
                    ProcessorErrorCodes.IdempotencyInProgress,
                    $"The first request with this {KaleidoIdempotencyHeaders.IdempotencyKey} is still running. Retry with the same key after {RetryAfterSeconds} second(s) to get its answer.");
            }

            return Error(
                StatusCodes.Status409Conflict,
                ProcessorErrorCodes.IdempotencyOutcomeUnknown,
                $"The first request with this {KaleidoIdempotencyHeaders.IdempotencyKey} never finished, so it may or may not have run; it is not run again. Read the process state and decide on the next step.");
        }

        http.Response.Headers[KaleidoIdempotencyHeaders.Replayed] = "true";

        return Results.Text(existing.Response, "application/json", Encoding.UTF8, existing.StatusCode);
    }

    // How long a caller waits before retrying a key that is still running.
    private const string RetryAfterSeconds = "1";

    private static bool IsSuccess(object? result) =>
        (result as IStatusCodeHttpResult)?.StatusCode is null or (>= 200 and < 300);

    private static IResult Error(int statusCode, string code, string message) =>
        Results.Json(new KaleidoErrorResponse([new KaleidoError(code, message)]), KaleidoJsonOptions.Options, statusCode: statusCode);
}
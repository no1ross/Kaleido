using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kaleido.Processor;
using Kaleido.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor;

/// <summary>
/// Makes process execution idempotent for a request sent with an <c>Idempotency-Key</c> header: the
/// key is admitted in the store before the step runs, the first successful answer is stored, and a
/// retry with the same key and the same request gets that answer again (with
/// <c>Idempotent-Replayed: true</c>) instead of running the step a second time (#285, ADR 0019).
/// </summary>
/// <remarks>
/// <para>
/// The key is scoped per service and caller (the caller's stable subject id, else its name), as a
/// canonical, length-framed tuple hashed to a fixed length (#514). A caller without an identity shares
/// its key space with every other such caller, so its key must be random: one shorter than 16
/// characters is refused with 400 <c>pro_invalid_idempotency_key</c>. Admission
/// is atomic in the store, so a request with a key that is still running does not run: 409
/// <c>pro_idempotency_in_progress</c> with <c>Retry-After</c>. The same key with a different request is
/// refused with 422 <c>pro_idempotency_key_reused</c>.
/// </para>
/// <para>
/// Each admission has its own id (#441), and the attempt finalizes only that admission: a 2xx answer is
/// stored (kept for <see cref="KaleidoHttpOptions.IdempotencyRetention"/>); a 4xx answer, or a refusal
/// raised before anything ran (validation, authorization, a concurrent change before the handler),
/// releases the key so it may be retried; anything else (a 5xx, an unexpected error, a cancellation, a
/// step that ran but whose outcome was not saved - a conflict after the handler included, #537) marks
/// the outcome unknown,
/// because the step may have run. A key whose outcome is unknown - marked so, or still running after
/// <see cref="KaleidoHttpOptions.IdempotencyLease"/> - is answered 409
/// <c>pro_idempotency_outcome_unknown</c> and not run again until
/// <see cref="KaleidoHttpOptions.IdempotencyUnknownOutcomeRetention"/> passes.
/// </para>
/// <para>
/// Finalization is written with <see cref="CancellationToken.None"/>: a request whose caller has gone (a
/// client timeout) still stores its outcome. It never changes the request's own answer: a store failure
/// is logged at Error, and a finalization the store no longer accepts (the admission lost its key) at
/// Warning. Without the header nothing changes. ASP.NET creates the filter per request, so a scoped
/// store (e.g. the SQLite store) is fine.
/// </para>
/// </remarks>
internal sealed partial class KaleidoIdempotencyEndpointFilter(
    IKaleidoIdempotencyStore store,
    KaleidoHttpOptions httpOptions,
    KaleidoServiceOptions serviceOptions,
    IKaleidoCorrelationContextAccessor correlationAccessor,
    TimeProvider time,
    ILogger<KaleidoIdempotencyEndpointFilter> logger)
    : IEndpointFilter
{
    private const int MaxKeyLength = 200;

    // A GUID's 32 hex digits pass; a counter or a word does not.
    private const int MinAnonymousKeyLength = 16;

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

        var caller = Identity(correlationAccessor.Current);

        if (string.Equals(caller.Kind, AnonymousKind, StringComparison.Ordinal) && key.Length < MinAnonymousKeyLength)
        {
            return Error(StatusCodes.Status400BadRequest, ProcessorErrorCodes.InvalidIdempotencyKey, $"A caller without an identity shares its {KaleidoIdempotencyHeaders.IdempotencyKey} space with every other such caller: send a random key of at least {MinAnonymousKeyLength} characters (a GUID).");
        }

        var now = time.GetUtcNow();
        var admission = new IdempotentResult
        {
            Key = Scope(caller, key),
            AdmissionId = Guid.NewGuid(),
            RequestHash = Hash(context),
            State = IdempotentResultState.InProgress,
            LeaseUntilUtc = now + httpOptions.IdempotencyLease,

            // Not freed by the replay retention: until it finishes, this attempt may run its step.
            ExpiresUtc = now + httpOptions.IdempotencyUnknownOutcomeRetention,
        };

        var existing = await store.AdmitAsync(admission, http.RequestAborted);

        if (existing is not null)
        {
            return Answer(http, existing, admission.RequestHash);
        }

        object? result;

        try
        {
            result = await next(context);
        }
        catch (Exception exception)
        {
            await FinalizeAsync(key, admission, IsRefusal(exception) ? Outcome.Refused : Outcome.Unknown, result: null);
            throw;
        }

        await FinalizeAsync(key, admission, OutcomeOf(result), result);

        return result;
    }

    private enum Outcome
    {
        Answered,
        Refused,
        Unknown,
    }

    // A 2xx answer is kept; a 4xx answer means the request was refused before anything was committed;
    // anything else (a 5xx) may have run.
    private static Outcome OutcomeOf(object? result) =>
        (result as IStatusCodeHttpResult)?.StatusCode switch
        {
            null or (>= 200 and < 300) => Outcome.Answered,
            >= 400 and < 500 => Outcome.Refused,
            _ => Outcome.Unknown,
        };

    // Raised before the step's work is committed, answered 4xx by the exception handler: the caller's
    // request was refused, so the key may be retried. Anything else may have run. A concurrency conflict
    // reaches here only from before the handler (the process's creation); after the handler it is a
    // StepOutcomeNotSavedException, an unknown outcome (#537, #514).
    private static bool IsRefusal(Exception exception) =>
        exception is KaleidoValidationException
            or KaleidoAuthorizationException
            or KaleidoConcurrencyException
            or Kaleido.Processor.Recovery.ProcessRecoveringException
            or BadHttpRequestException
            or JsonException
            or ArgumentException;

    // The attempt's own admission is finalized, even when the caller has gone; the request's answer
    // never depends on it.
    private async Task FinalizeAsync(string key, IdempotentResult admission, Outcome outcome, object? result)
    {
        bool accepted;

        try
        {
            accepted = outcome switch
            {
                Outcome.Answered => await store.CompleteAsync(Answered(admission, result), CancellationToken.None),
                Outcome.Refused => await store.ReleaseAsync(admission.Key, admission.AdmissionId, CancellationToken.None),
                _ => await store.CompleteAsync(Unknown(admission), CancellationToken.None),
            };
        }
        // Finalized with CancellationToken.None: no cancellation of the caller reaches here, so any failure -
        // an OperationCanceledException of the store's own (a timeout) included - is logged (#517).
        catch (Exception exception)
        {
            LogFinalizationFailed(logger, exception, key, admission.Key, outcome);
            return;
        }

        if (!accepted)
        {
            LogFinalizationNotAccepted(logger, key, admission.Key, outcome);
        }
    }

    private IdempotentResult Answered(IdempotentResult admission, object? result) =>
        admission with
        {
            State = IdempotentResultState.Completed,
            StatusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK,
            Response = result is IValueHttpResult value ? JsonSerializer.Serialize(value.Value, KaleidoJsonOptions.Options) : string.Empty,
            ExpiresUtc = time.GetUtcNow() + httpOptions.IdempotencyRetention,
        };

    private IdempotentResult Unknown(IdempotentResult admission) =>
        admission with
        {
            State = IdempotentResultState.OutcomeUnknown,
            ExpiresUtc = time.GetUtcNow() + httpOptions.IdempotencyUnknownOutcomeRetention,
        };

    // The stored record's key is a hash of the caller's scope: the log names both, so an operator finds
    // the record.
    [LoggerMessage(Level = LogLevel.Error, Message = "The outcome ({Outcome}) of the request with Idempotency-Key '{IdempotencyKey}' (record '{RecordKey}') could not be stored; the request's own answer is unchanged.")]
    private static partial void LogFinalizationFailed(ILogger logger, Exception exception, string idempotencyKey, string recordKey, Outcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The outcome ({Outcome}) of the request with Idempotency-Key '{IdempotencyKey}' (record '{RecordKey}') was not stored: its admission no longer holds the key (it expired, or the key was admitted again).")]
    private static partial void LogFinalizationNotAccepted(ILogger logger, string idempotencyKey, string recordKey, Outcome outcome);
    private const string AnonymousKind = "anonymous";

    // Scoped per service and caller: two callers cannot see each other's answers. The caller is its
    // stable subject id when the principal has one (a renamed user keeps their keys; two users with the
    // same display name stay apart), else its name; its kind keeps an id and a name from colliding.
    // Version 1 (#514): the tuple {service, identity kind, identity, key}, each part length-framed so no
    // two tuples read the same whatever characters they contain, hashed to a fixed length. Records
    // admitted in the earlier "service:caller:key" format no longer match; they expire by their
    // retention.
    private string Scope((string Kind, string Value) caller, string key) =>
        RecordKey(serviceOptions.ServiceName, caller.Kind, caller.Value, key);

    // The stored record's key for a scope: what an operator looks for, and what the tests expect.
    internal static string RecordKey(string service, string identityKind, string identity, string key)
    {
        var framed = string.Concat(Frame(service), Frame(identityKind), Frame(identity), Frame(key));

        return "v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(framed))).ToLowerInvariant();
    }

    private static string Frame(string part) => string.Concat(part.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ":", part);

    private static (string Kind, string Value) Identity(KaleidoCorrelationContext caller) =>
        caller.CallerId is { } id ? ("id", id)
        : caller.CallerName is { } name ? ("name", name)
        : (AnonymousKind, string.Empty);

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
    private IResult Answer(HttpContext http, IdempotentResult existing, string requestHash)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Error(
                StatusCodes.Status422UnprocessableEntity,
                ProcessorErrorCodes.IdempotencyKeyReused,
                $"The {KaleidoIdempotencyHeaders.IdempotencyKey} was already used with a different request. Use a new key for a new call.");
        }

        if (existing.State == IdempotentResultState.InProgress && existing.LeaseUntilUtc > time.GetUtcNow())
        {
            http.Response.Headers.RetryAfter = RetryAfterSeconds;

            return Error(
                StatusCodes.Status409Conflict,
                ProcessorErrorCodes.IdempotencyInProgress,
                $"The first request with this {KaleidoIdempotencyHeaders.IdempotencyKey} is still running. Retry with the same key after {RetryAfterSeconds} second(s) to get its answer.");
        }

        // Still in progress after its lease (it never finished), or failed after it may have run.
        if (existing.State != IdempotentResultState.Completed)
        {
            return Error(
                StatusCodes.Status409Conflict,
                ProcessorErrorCodes.IdempotencyOutcomeUnknown,
                $"The first request with this {KaleidoIdempotencyHeaders.IdempotencyKey} did not finish with an answer, so it may or may not have run; it is not run again with this key. Read the process state and decide on the next step (a new call takes a new key).");
        }

        http.Response.Headers[KaleidoIdempotencyHeaders.Replayed] = "true";

        // The process id travels in the header as on the first answer (#514): a new process's id is
        // known only from the stored answer.
        if (ProcessIdOf(existing.Response) is { } processId)
        {
            http.Response.Headers[KaleidoCorrelationHeaders.ProcessId] = processId;
        }

        return Results.Text(existing.Response, "application/json", Encoding.UTF8, existing.StatusCode);
    }

    private static string? ProcessIdOf(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(response);

            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("processId", out var processId) &&
                processId.ValueKind == JsonValueKind.String &&
                Guid.TryParse(processId.GetString(), out var id)
                    ? id.ToString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // How long a caller waits before retrying a key that is still running.
    private const string RetryAfterSeconds = "1";

    private static IResult Error(int statusCode, string code, string message) =>
        Results.Json(new KaleidoErrorResponse([new KaleidoError(code, message)]), KaleidoJsonOptions.Options, statusCode: statusCode);
}

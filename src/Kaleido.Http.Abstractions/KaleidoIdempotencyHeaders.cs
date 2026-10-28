namespace Kaleido.Http;

/// <summary>
/// HTTP header names for idempotent process execution: a request sent again with the same
/// <see cref="IdempotencyKey"/> gets the original answer instead of running the step a second time.
/// </summary>
public static class KaleidoIdempotencyHeaders
{
    /// <summary>
    /// Request: a caller-chosen key for one logical execute call (e.g. a GUID), the same on every retry
    /// of that call. Scoped per service and caller (subject id, else name); kept for <c>KaleidoHttpOptions.IdempotencyRetention</c>.
    /// </summary>
    public const string IdempotencyKey =
        "Idempotency-Key";

    /// <summary>Response: the value "true" when the answer is the stored answer of an earlier request with the same key.</summary>
    public const string Replayed =
        "Idempotent-Replayed";
}

namespace Kaleido.Storage;

/// <summary>
/// The store's record of one idempotency key: admitted and in progress while its attempt runs, then
/// completed with the answer a retry with the same key gets again instead of running the step a
/// second time - or marked as having an unknown outcome when the attempt failed after it may have run.
/// </summary>
/// <remarks>
/// <para>
/// Kept by the <see cref="IKaleidoIdempotencyStore"/> (the SQLite reference store: its own table in
/// the same database) until <see cref="ExpiresUtc"/>. The transport decides what the key's scope is and
/// what the answer looks like; the store only keeps it.
/// </para>
/// <para>
/// Lifecycle (ADR 0019, #441): <see cref="IKaleidoIdempotencyStore.AdmitAsync"/> creates the record in
/// <see cref="IdempotentResultState.InProgress"/> before the step runs, atomically, with a fresh
/// <see cref="AdmissionId"/>. The attempt then finalizes its own admission only - matched by key
/// <em>and</em> admission id, so an attempt that outlived its record never touches the record of a
/// later admission of the same key:
/// </para>
/// <list type="bullet">
/// <item><description>a successful answer completes it (<see cref="IdempotentResultState.Completed"/>),
/// kept for the replay retention;</description></item>
/// <item><description>a refusal made before anything ran (a validation, authorization or concurrency
/// error) releases it, so the key may be used again;</description></item>
/// <item><description>any other failure marks it <see cref="IdempotentResultState.OutcomeUnknown"/>: the
/// step may have run, so the key is not run again until the record expires.</description></item>
/// </list>
/// <para>
/// A record still in progress after <see cref="LeaseUntilUtc"/> belongs to an attempt that never
/// finished (e.g. the service stopped); it is treated like an unknown outcome. To resolve one, an
/// operator reads the process state and, once the outcome is known, deletes the record (or waits for
/// it to expire); the caller can also continue with a new key.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record IdempotentResult
{
    /// <summary>The scoped idempotency key (unique per service and caller).</summary>
    public required string Key { get; init; }

    /// <summary>
    /// Identifies one admission of <see cref="Key"/> (a fencing token): set when the key is admitted, and
    /// required to complete or release it, so only the attempt that holds the record can finalize it.
    /// </summary>
    public required Guid AdmissionId { get; init; }

    /// <summary>A hash of the request the key was first used with; the same key with another request is refused.</summary>
    public required string RequestHash { get; init; }

    /// <summary>Whether the attempt is still running, completed with an answer, or ended with an unknown outcome.</summary>
    public required IdempotentResultState State { get; init; }

    /// <summary>
    /// While <see cref="IdempotentResultState.InProgress"/>: until when the attempt counts as running.
    /// After it, an attempt that has not finished is treated as abandoned (its outcome is unknown).
    /// </summary>
    public DateTimeOffset LeaseUntilUtc { get; init; }

    /// <summary>The status of the stored answer (on HTTP, the response status code); 0 unless completed.</summary>
    public int StatusCode { get; init; }

    /// <summary>The stored answer as it was returned (on HTTP, the JSON response body); empty unless completed.</summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>
    /// When the record is no longer used and may be deleted; the key can then be admitted again. A
    /// completed answer is kept for the replay retention; an admission in progress, and an unknown
    /// outcome, for the (longer) unknown-outcome retention, so an attempt that may have run is never
    /// freed by the replay retention.
    /// </summary>
    public required DateTimeOffset ExpiresUtc { get; init; }
}

/// <summary>The state of an <see cref="IdempotentResult"/>.</summary>
public enum IdempotentResultState
{
    /// <summary>Admitted: the attempt with the key is running (or was abandoned after its lease).</summary>
    InProgress = 0,

    /// <summary>The attempt completed; <see cref="IdempotentResult.Response"/> is its answer.</summary>
    Completed = 1,

    /// <summary>
    /// The attempt failed after its step may have run (an unexpected error, a cancellation): it is not
    /// run again with the key; a retry is told the outcome is unknown.
    /// </summary>
    OutcomeUnknown = 2,
}

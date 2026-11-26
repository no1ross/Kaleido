namespace Kaleido.Process.Server.Idempotency;

/// <summary>
/// The store's record of one idempotency key: admitted and in progress while its attempt runs, then
/// completed with the answer a retry with the same key gets again instead of running the step a
/// second time - or marked as having an unknown outcome when the attempt failed after it may have run.
/// </summary>
/// <remarks>
/// <para>
/// Kept by the <see cref="IProcessIdempotencyStore"/> until <see cref="ExpiresUtc"/>.
/// The caller chooses the scope of the key and the replay's format; the store treats payload bytes as opaque.
/// </para>
/// <para>
/// Lifecycle (ADR 0019, #441): <see cref="IProcessIdempotencyStore.AdmitAsync"/> creates the record in
/// <see cref="IdempotencyState.InProgress"/> before the step runs, atomically, with a fresh
/// <see cref="AdmissionId"/>. The attempt then finalizes its own admission only - matched by key
/// <em>and</em> admission id, so an attempt that outlived its record never touches the record of a
/// later admission of the same key:
/// </para>
/// <list type="bullet">
/// <item><description>a successful answer completes it (<see cref="IdempotencyState.Completed"/>),
/// kept for the replay retention;</description></item>
/// <item><description>a refusal made before anything ran (a validation, authorization or concurrency
/// error) releases it, so the key may be used again;</description></item>
/// <item><description>any other failure marks it <see cref="IdempotencyState.OutcomeUnknown"/>: the
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
public sealed record IdempotencyRecord
{
    /// <summary>The scoped idempotency key (unique per service, service version and caller).</summary>
    public required string Key { get; init; }

    /// <summary>
    /// The processor whose step the key ran (<see cref="KaleidoServiceOptions.ServiceName"/>). Stored as given,
    /// so an operator can find a service's records in a database shared by several services (#552); the
    /// scope itself is already part of <see cref="Key"/>.
    /// </summary>
    public string ProcessorName { get; init; } = string.Empty;

    /// <summary>
    /// The version of <see cref="ProcessorName"/> (<see cref="KaleidoServiceOptions.ServiceVersion"/>);
    /// <see langword="null"/> when it is unversioned. Stored as given, like <see cref="ProcessorName"/>.
    /// </summary>
    public string? ProcessorVersion { get; init; }

    /// <summary>
    /// Identifies one admission of <see cref="Key"/> (a fencing token): set when the key is admitted, and
    /// required to complete or release it, so only the attempt that holds the record can finalize it.
    /// </summary>
    public required Guid AdmissionId { get; init; }

    /// <summary>A hash of the request the key was first used with; the same key with another request is refused.</summary>
    public required string RequestHash { get; init; }

    /// <summary>Whether the attempt is still running, completed with an answer, or ended with an unknown outcome.</summary>
    public required IdempotencyState State { get; init; }

    /// <summary>
    /// While <see cref="IdempotencyState.InProgress"/>: until when the attempt counts as running.
    /// After it, an attempt that has not finished is treated as abandoned (its outcome is unknown).
    /// </summary>
    public DateTimeOffset LeaseUntilUtc { get; init; }

    /// <summary>The opaque replay value; present exactly when <see cref="State"/> is completed.</summary>
    public IdempotencyReplay? Replay { get; init; }

    /// <summary>
    /// When the record is no longer used and may be deleted; the key can then be admitted again. A
    /// completed answer is kept for the replay retention; an admission in progress, and an unknown
    /// outcome, for the (longer) unknown-outcome retention, so an attempt that may have run is never
    /// freed by the replay retention.
    /// </summary>
    public required DateTimeOffset ExpiresUtc { get; init; }
}

/// <summary>The state of an <see cref="IdempotencyRecord"/>.</summary>
public enum IdempotencyState
{
    /// <summary>Admitted: the attempt with the key is running (or was abandoned after its lease).</summary>
    InProgress = 0,

    /// <summary>The attempt completed; <see cref="IdempotencyRecord.Replay"/> is its answer.</summary>
    Completed = 1,

    /// <summary>
    /// The attempt failed after its step may have run (an unexpected error, a cancellation): it is not
    /// run again with the key; a retry is told the outcome is unknown.
    /// </summary>
    OutcomeUnknown = 2,
}

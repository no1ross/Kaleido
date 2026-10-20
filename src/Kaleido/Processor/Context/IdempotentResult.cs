namespace Kaleido.Processor.Context;

/// <summary>
/// The store's record of one idempotency key: admitted and in progress while the first attempt runs,
/// then completed with the answer that a retry with the same key gets again instead of running the
/// step a second time.
/// </summary>
/// <remarks>
/// <para>
/// Kept by the <see cref="IKaleidoContextStore"/> next to the process state (the SQLite reference
/// store: its own table in the same database) until <see cref="ExpiresUtc"/>. The transport decides
/// what the key's scope is and what the answer looks like; the store only keeps it.
/// </para>
/// <para>
/// Lifecycle: <see cref="IKaleidoContextStore.AdmitAsync"/> creates the record in
/// <see cref="IdempotentResultState.InProgress"/> before the step runs, atomically, so a second
/// attempt with the same key never runs alongside the first. A successful answer is stored with
/// <see cref="IKaleidoContextStore.CompleteAsync"/> (<see cref="IdempotentResultState.Completed"/>);
/// any other outcome removes the record with <see cref="IKaleidoContextStore.ReleaseAsync"/>, so the
/// key may be used again. A record still in progress after <see cref="LeaseUntilUtc"/> belongs to
/// an attempt that never finished (e.g. the service stopped): it may have run, so it is never run
/// again with that key.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record IdempotentResult
{
    /// <summary>The scoped idempotency key (unique per service and caller).</summary>
    public required string Key { get; init; }

    /// <summary>A hash of the request the key was first used with; the same key with another request is refused.</summary>
    public required string RequestHash { get; init; }

    /// <summary>Whether the first attempt is still running or has completed with an answer.</summary>
    public required IdempotentResultState State { get; init; }

    /// <summary>
    /// While <see cref="IdempotentResultState.InProgress"/>: until when the attempt counts as running.
    /// After it, an attempt that has not completed is treated as abandoned.
    /// </summary>
    public DateTimeOffset LeaseUntilUtc { get; init; }

    /// <summary>The status of the stored answer (on HTTP, the response status code); 0 while in progress.</summary>
    public int StatusCode { get; init; }

    /// <summary>The stored answer as it was returned (on HTTP, the JSON response body); empty while in progress.</summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>When the record is no longer used and may be deleted; the key can then be admitted again.</summary>
    public required DateTimeOffset ExpiresUtc { get; init; }
}

/// <summary>The state of an <see cref="IdempotentResult"/>.</summary>
public enum IdempotentResultState
{
    /// <summary>Admitted: the first attempt with the key is running (or was abandoned after its lease).</summary>
    InProgress = 0,

    /// <summary>The first attempt completed; <see cref="IdempotentResult.Response"/> is its answer.</summary>
    Completed = 1,
}

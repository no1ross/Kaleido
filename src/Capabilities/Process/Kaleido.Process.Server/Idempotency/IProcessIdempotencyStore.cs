namespace Kaleido.Process.Server.Idempotency;

/// <summary>
/// Atomic admission and fenced completion for a caller-scoped key. The store does not interpret
/// the key, request hash or replay bytes and need not share a transaction with process state.
/// </summary>
/// <remarks>
/// Within a record's lifetime, only one attempt can be admitted. A retry observes an in-progress
/// attempt, a completed replay, or an unknown outcome. An expired record may be admitted again;
/// admission IDs prevent a late attempt from changing the replacement. An in-progress record beyond
/// its lease is treated as an unknown outcome until expiry, never silently re-admitted.
/// Durable implementations must retain unknown outcomes across restarts.
/// </remarks>
public interface IProcessIdempotencyStore
{
    /// <summary>
    /// Atomically inserts an in-progress admission when the key is absent or expired; otherwise
    /// returns the existing record without changing it. Only one concurrent caller receives null.
    /// </summary>
    /// <param name="admission">An in-progress record with an admission ID, lease and expiry.</param>
    /// <param name="cancellationToken">Cancels the admission.</param>
    /// <returns>Null if admitted, otherwise the record holding the key.</returns>
    Task<IdempotencyRecord?> AdmitAsync(IdempotencyRecord admission, CancellationToken cancellationToken);

    /// <summary>
    /// Conditionally replaces the in-progress record with a completed replay or unknown outcome,
    /// matching both key and admission ID. Never inserts a missing record.
    /// </summary>
    /// <param name="result">The final record, retaining the admitted key and admission ID.</param>
    /// <param name="cancellationToken">Cancels the write; callers can use <see cref="CancellationToken.None"/> to persist an outcome after a caller disconnects.</param>
    /// <returns>Whether this admission still held the key and was finalized.</returns>
    Task<bool> CompleteAsync(IdempotencyRecord result, CancellationToken cancellationToken);

    /// <summary>Removes an in-progress admission that was refused before any work ran.</summary>
    /// <param name="key">The scoped key.</param>
    /// <param name="admissionId">The admission ID fencing this removal.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>Whether the admission still held the key and was removed.</returns>
    Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken);
}

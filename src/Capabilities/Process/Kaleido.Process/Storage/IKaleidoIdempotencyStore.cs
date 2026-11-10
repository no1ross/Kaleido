namespace Kaleido.Storage;

/// <summary>
/// The records of requests sent with an idempotency key: admitted before the request runs, then
/// finalized by the attempt that holds the admission (ADR 0019, #441). Registered by <c>AddKaleido()</c>
/// for every service; used by a transport (on HTTP, the <c>Idempotency-Key</c> header).
/// </summary>
/// <remarks>
/// <para>
/// One of the three store contracts, next to <see cref="IKaleidoContextStore"/> and
/// <see cref="IKaleidoOutboxStore"/>. It needs no transaction with them, so it may live in their
/// database (the SQLite reference store: its own table there) or anywhere else that offers an atomic
/// insert-if-absent and a conditional update. The default keeps the records in memory.
/// </para>
/// <para>
/// <strong>Guarantee:</strong> within a record's lifetime, a key runs its step at most once. A retry
/// gets the stored answer, is told the attempt is still running, or is told the outcome is unknown.
/// Each admission carries an <see cref="IdempotentResult.AdmissionId"/>: <see cref="CompleteAsync"/> and
/// <see cref="ReleaseAsync"/> act only on the record of that admission, so an attempt that outlived its
/// record (it expired and the key was admitted again) can neither overwrite nor delete the newer one.
/// </para>
/// <para>
/// Keep records across restarts: a record still <see cref="IdempotentResultState.InProgress"/> after its
/// <see cref="IdempotentResult.LeaseUntilUtc"/>, or one marked
/// <see cref="IdempotentResultState.OutcomeUnknown"/>, is how Kaleido knows an attempt may have run, and
/// it then does not run that key again until the record expires. An operator resolves such a key by
/// reading the process state and deleting the record.
/// </para>
/// </remarks>
public interface IKaleidoIdempotencyStore
{
    /// <summary>
    /// Admits an idempotency key before its request runs: stores <paramref name="admission"/> (state
    /// <see cref="IdempotentResultState.InProgress"/>, with its admission id) when no record exists for
    /// its key, or only an expired one (<see cref="IdempotentResult.ExpiresUtc"/> passed), and returns
    /// null. Otherwise stores nothing and returns the existing record - the attempt still in progress,
    /// the completed answer, or the unknown outcome.
    /// </summary>
    /// <param name="admission">The record to store for the key, in progress, with its admission id, lease and expiry.</param>
    /// <param name="cancellationToken">Cancels the admission.</param>
    /// <returns>Null when the key was admitted; otherwise the record that already holds the key.</returns>
    /// <remarks>
    /// Must be one atomic insert-if-absent (a unique key with <c>INSERT ... ON CONFLICT DO NOTHING</c>,
    /// a conditional put): two requests admitting the same key at the same time never both get null.
    /// That is what keeps a retry from running alongside the first attempt. A store may delete
    /// expired records here.
    /// </remarks>
    Task<IdempotentResult?> AdmitAsync(IdempotentResult admission, CancellationToken cancellationToken);

    /// <summary>
    /// Finalizes an admission: replaces the record of <paramref name="result"/>'s key with it - state
    /// <see cref="IdempotentResultState.Completed"/> (with the answer a retry then gets) or
    /// <see cref="IdempotentResultState.OutcomeUnknown"/> - only when that record is still
    /// <see cref="IdempotentResultState.InProgress"/> under the same <see cref="IdempotentResult.AdmissionId"/>.
    /// </summary>
    /// <param name="result">The finalized record, with the admission id of the attempt and its new expiry.</param>
    /// <param name="cancellationToken">Cancels the save. Kaleido passes <see cref="CancellationToken.None"/>: the outcome of an attempt whose caller has gone must still be stored.</param>
    /// <returns>
    /// True when the record was replaced; false when the admission no longer holds the key (the record
    /// expired and was removed, or the key was admitted again) - nothing is written then.
    /// </returns>
    /// <remarks>One conditional write (<c>UPDATE ... WHERE Key = @key AND AdmissionId = @id AND State = InProgress</c>); never an insert.</remarks>
    Task<bool> CompleteAsync(IdempotentResult result, CancellationToken cancellationToken);

    /// <summary>
    /// Releases an admission whose request was refused before anything ran (so the key may be used
    /// again): removes the record of <paramref name="key"/> only when it is still
    /// <see cref="IdempotentResultState.InProgress"/> under <paramref name="admissionId"/>.
    /// </summary>
    /// <param name="key">The scoped idempotency key.</param>
    /// <param name="admissionId">The admission id of the attempt releasing it.</param>
    /// <param name="cancellationToken">Cancels the removal. Kaleido passes <see cref="CancellationToken.None"/>.</param>
    /// <returns>True when the record was removed; false when the admission no longer holds the key.</returns>
    Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken);
}

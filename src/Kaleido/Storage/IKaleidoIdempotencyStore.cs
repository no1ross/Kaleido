using Kaleido.Processor.Context;

namespace Kaleido.Storage;

/// <summary>
/// The records of requests sent with an idempotency key: admitted before the request runs, then
/// completed with its answer or released (ADR 0019). Registered by <c>AddKaleido()</c> for every
/// service; used by a transport (on HTTP, the <c>Idempotency-Key</c> header).
/// </summary>
/// <remarks>
/// <para>
/// One of the three store contracts, next to <see cref="IKaleidoContextStore"/> and
/// <see cref="IKaleidoOutboxStore"/>. It needs no transaction with them, so it may live in their
/// database (the SQLite reference store: its own table there) or anywhere else that offers an atomic
/// insert-if-absent. The default keeps the records in memory.
/// </para>
/// <para>
/// Keep records across restarts: a record still <see cref="IdempotentResultState.InProgress"/> after
/// its <see cref="IdempotentResult.LeaseUntilUtc"/> is how Kaleido knows an attempt never finished, and
/// it then never runs that key again.
/// </para>
/// </remarks>
public interface IKaleidoIdempotencyStore
{
    /// <summary>
    /// Admits an idempotency key before its request runs: stores <paramref name="admission"/> (state
    /// <see cref="IdempotentResultState.InProgress"/>) when no record exists for its key, or only an
    /// expired one (<see cref="IdempotentResult.ExpiresUtc"/> passed), and returns null. Otherwise
    /// stores nothing and returns the existing record - the attempt still in progress, or the
    /// completed answer.
    /// </summary>
    /// <param name="admission">The record to store for the key, in progress, with its lease and expiry.</param>
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
    /// Stores the answer for an admitted key (state <see cref="IdempotentResultState.Completed"/>),
    /// replacing its in-progress record; a retry with the key then gets this answer.
    /// </summary>
    /// <param name="result">The completed record, with the answer and its expiry.</param>
    /// <param name="cancellationToken">Cancels the save. Kaleido passes <see cref="CancellationToken.None"/>: the answer of an attempt whose caller has gone must still be stored.</param>
    Task CompleteAsync(IdempotentResult result, CancellationToken cancellationToken);

    /// <summary>
    /// Removes an admitted key that did not complete with an answer to keep (its request failed),
    /// so the key may be used again. Removes only a record still in progress; a completed one stays.
    /// </summary>
    /// <param name="key">The scoped idempotency key.</param>
    /// <param name="cancellationToken">Cancels the removal. Kaleido passes <see cref="CancellationToken.None"/>.</param>
    Task ReleaseAsync(string key, CancellationToken cancellationToken);
}

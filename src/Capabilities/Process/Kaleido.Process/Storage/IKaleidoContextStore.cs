using Kaleido.Processor.Context;

namespace Kaleido.Storage;

/// <summary>
/// The durable process state of a Kaleido service: each process's resumable state, saved with
/// optimistic concurrency. Registered by <c>AddKaleido()</c> for every service.
/// </summary>
/// <remarks>
/// <para>
/// Process state only. Events are the outbox's (<see cref="IKaleidoOutboxStore"/>), and the
/// idempotent requests are <see cref="IKaleidoIdempotencyStore"/>'s. The default keeps state in
/// memory, suitable for development and testing only: nothing survives a restart, and state grows
/// without bound in long-running services. Register a durable implementation before deploying to
/// production (e.g. <c>AddSqliteContextStore</c>, or your own with <c>AddContextStore&lt;T&gt;()</c>).
/// </para>
/// <para>
/// <strong>Transitions:</strong> Kaleido calls <see cref="SaveAsync"/> inside the transaction of
/// <see cref="IKaleidoTransactions"/> and adds the transition's events to the outbox in the same
/// transaction, then commits (ADR 0023). An implementation writes the state in that transaction when
/// one is open on its database (e.g. the shared <c>DbContext</c>), and on its own otherwise.
/// </para>
/// <para>
/// <strong>Scope (#552):</strong> a process is stored under its <see cref="ProcessKey"/> - the owning
/// processor, its version and the process id. Several processors, and several versions of one, may share
/// a store: every read and write must match all three parts, and the same process id under another
/// processor or version is another process (the origin and the target of a hand-off).
/// </para>
/// <para>
/// <strong>Concurrency (optimistic):</strong> every save is conditional on
/// <see cref="ProcessorContext.Version"/>. Two concurrent requests for the same process cannot
/// overwrite each other: the second save is refused with <see cref="KaleidoConcurrencyException"/>
/// and the caller gets 409 <c>pro_process_conflict</c>.
/// </para>
/// </remarks>
public interface IKaleidoContextStore
{
    /// <summary>
    /// The process state stored under <paramref name="key"/>, or null when there is none (never throws for
    /// a missing one).
    /// </summary>
    /// <param name="key">The owning processor, its version and the process id; all three must match.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    Task<ProcessorContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Saves <paramref name="context"/> under its <see cref="ProcessorContext.Key"/> when the stored version
    /// still equals <see cref="ProcessorContext.Version"/> (0: the process must not exist yet), stores it
    /// with the version incremented, and returns the saved context (with the new version).
    /// </summary>
    /// <param name="context">The process state to save.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <exception cref="KaleidoConcurrencyException">
    /// The stored version differs - another request saved the process since it was loaded, or a process
    /// with that key already exists for a context with version 0. Nothing is saved, and Kaleido adds none
    /// of the transition's events.
    /// </exception>
    Task<ProcessorContext> SaveAsync(
        ProcessorContext context,
        CancellationToken cancellationToken);
}

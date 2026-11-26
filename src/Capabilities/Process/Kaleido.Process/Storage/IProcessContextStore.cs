using Kaleido.Process.Context;

namespace Kaleido.Process.Storage;

/// <summary>
/// The durable process state of a Kaleido service: each process's resumable state, saved with
/// optimistic concurrency. A host selects an implementation explicitly when it runs Process steps.
/// </summary>
/// <remarks>
/// <para>
/// Process state only. Events are the outbox's (<see cref="IKaleidoOutboxStore"/>); request
/// admission is owned separately by server-side components. An explicitly chosen in-memory store
/// is suitable for development and testing only: nothing survives a restart, and state grows
/// without bound in long-running services. Register a durable implementation before deploying to
/// production (for example your own with <c>AddContextStore&lt;T&gt;()</c>).
/// </para>
/// <para>
/// <strong>Transitions:</strong> Kaleido calls <see cref="SaveAsync"/> inside the transaction of
/// <see cref="IProcessTransactions"/> and adds the transition's events to the outbox in the same
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
/// <see cref="ProcessContext.Version"/>. Two concurrent requests for the same process cannot
/// overwrite each other: the second save is refused with <see cref="ProcessConcurrencyException"/>
/// and the adapter reports <c>pro_process_conflict</c> to the caller.
/// </para>
/// </remarks>
public interface IProcessContextStore
{
    /// <summary>
    /// The process state stored under <paramref name="key"/>, or null when there is none (never throws for
    /// a missing one).
    /// </summary>
    /// <param name="key">The owning processor, its version and the process id; all three must match.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    Task<ProcessContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Saves <paramref name="context"/> under its <see cref="ProcessContext.Key"/> when the stored version
    /// still equals <see cref="ProcessContext.Version"/> (0: the process must not exist yet), stores it
    /// with the version incremented, and returns the saved context (with the new version).
    /// </summary>
    /// <param name="context">The process state to save.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <exception cref="ProcessConcurrencyException">
    /// The stored version differs - another request saved the process since it was loaded, or a process
    /// with that key already exists for a context with version 0. Nothing is saved, and Kaleido adds none
    /// of the transition's events.
    /// </exception>
    Task<ProcessContext> SaveAsync(
        ProcessContext context,
        CancellationToken cancellationToken);
}

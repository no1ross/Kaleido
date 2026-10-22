using Kaleido.Processor.Context;

namespace Kaleido.Storage;

/// <summary>
/// The durable process state of a Kaleido service: each process's resumable state, saved with
/// optimistic concurrency. Registered by <c>AddKaleido()</c> for every service.
/// </summary>
/// <remarks>
/// <para>
/// Process state only. Events are the outbox integration's (<see cref="IKaleidoOutbox"/>), and the
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
/// <strong>Concurrency (optimistic):</strong> every save is conditional on
/// <see cref="ProcessorContext.Version"/>. Two concurrent requests for the same process cannot
/// overwrite each other: the second save is refused with <see cref="KaleidoConcurrencyException"/>
/// and the caller gets 409 <c>pro_process_conflict</c>.
/// </para>
/// </remarks>
public interface IKaleidoContextStore
{
    /// <summary>The process state, or null when no process with that id exists (never throws for a missing one).</summary>
    Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves <paramref name="context"/> when the stored version still equals
    /// <see cref="ProcessorContext.Version"/> (0: the process must not exist yet), stores it with the
    /// version incremented, and returns the saved context (with the new version).
    /// </summary>
    /// <param name="context">The process state to save.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <exception cref="KaleidoConcurrencyException">
    /// The stored version differs - another request saved the process since it was loaded, or a process
    /// with that id already exists for a context with version 0. Nothing is saved, and Kaleido adds none
    /// of the transition's events.
    /// </exception>
    Task<ProcessorContext> SaveAsync(
        ProcessorContext context,
        CancellationToken cancellationToken);
}

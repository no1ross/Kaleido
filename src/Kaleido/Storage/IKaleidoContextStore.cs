using Kaleido.Processor.Context;

namespace Kaleido.Storage;

/// <summary>
/// The durable process state of a Kaleido service: each process's resumable state, saved with
/// optimistic concurrency. Registered by <c>AddKaleido()</c> for every service.
/// </summary>
/// <remarks>
/// <para>
/// One of the three store contracts, each with one concern: process state (this one), the event
/// outbox (<see cref="IKaleidoOutboxStore"/>) and idempotent requests
/// (<see cref="IKaleidoIdempotencyStore"/>). The defaults keep everything in memory, suitable for
/// development and testing only: nothing survives a restart, and process state grows without bound in
/// long-running services. Register durable implementations before deploying to production (e.g.
/// <c>UseSqliteKaleidoContextStore</c>, which registers all three on one database, or your own).
/// </para>
/// <para>
/// <strong>Same database as the outbox:</strong> <see cref="SaveAsync"/> adds the events that
/// describe a save to the outbox in the same transaction as the state, so an implementation of this
/// contract and of <see cref="IKaleidoOutboxStore"/> must share their database (e.g. one
/// <c>DbContext</c>). Replace both together.
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
    Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves <paramref name="context"/> when the stored version still equals
    /// <see cref="ProcessorContext.Version"/> (0: the process must not exist yet), stores it with the
    /// version incremented, adds <paramref name="events"/> to the outbox - all in one transaction:
    /// state and events are saved together or not at all - and returns the saved context (with the
    /// new version).
    /// </summary>
    /// <param name="context">The process state to save.</param>
    /// <param name="events">
    /// Outbox entries describing this save (may be empty), written to the outbox that
    /// <see cref="IKaleidoOutboxStore"/> claims from.
    /// </param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <exception cref="KaleidoConcurrencyException">
    /// The stored version differs - another request saved the process since it was loaded, or a process
    /// with that id already exists for a context with version 0. Nothing is saved, no event is added.
    /// </exception>
    Task<ProcessorContext> SaveAsync(
        ProcessorContext context,
        IReadOnlyCollection<KaleidoOutboxEvent> events,
        CancellationToken cancellationToken = default);
}

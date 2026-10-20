using Kaleido.Processor.Context;

namespace Kaleido.Storage;

/// <summary>
/// The one durable store of a Kaleido service: process state, the stored answers of idempotent
/// requests, and the event outbox. Registered by <c>AddKaleido()</c> for every service.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation keeps everything in memory, suitable for development and testing
/// only: nothing survives a restart, and process state grows without bound in long-running
/// services. Register a durable implementation before deploying to production (e.g.
/// <c>UseSqliteKaleidoContextStore</c> or a custom store) - all three parts then live in the same
/// database, so there is nothing extra to set up.
/// </para>
/// <para>
/// <strong>Concurrency (optimistic):</strong> every save is conditional on
/// <see cref="ProcessorContext.Version"/>. Two concurrent requests for the same process cannot
/// overwrite each other: the second save is refused with <see cref="KaleidoConcurrencyException"/>
/// and the caller gets 409 <c>pro_process_conflict</c>.
/// </para>
/// <para>
/// <strong>Event outbox (at least once):</strong> Kaleido never publishes events directly. It writes
/// them to the outbox - process events that describe a save in the same transaction as the state
/// (<see cref="SaveAsync"/>), others right after (<see cref="EnqueueEventsAsync"/>) - and a
/// background relay hands each one to <see cref="IEventPublisher"/>, retrying until it succeeds.
/// With several instances on one shared store, each relay claims entries
/// (<see cref="ClaimEventsAsync"/>) before publishing them; a claim must be an atomic
/// compare-and-set, so an entry is never in two instances' hands at once.
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
    /// <param name="events">Outbox entries describing this save (may be empty).</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <exception cref="KaleidoConcurrencyException">
    /// The stored version differs - another request saved the process since it was loaded, or a process
    /// with that id already exists for a context with version 0. Nothing is saved, no event is added.
    /// </exception>
    Task<ProcessorContext> SaveAsync(
        ProcessorContext context,
        IReadOnlyCollection<KaleidoOutboxEvent> events,
        CancellationToken cancellationToken = default);

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
    Task<IdempotentResult?> AdmitAsync(IdempotentResult admission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the answer for an admitted key (state <see cref="IdempotentResultState.Completed"/>),
    /// replacing its in-progress record; a retry with the key then gets this answer.
    /// </summary>
    /// <param name="result">The completed record, with the answer and its expiry.</param>
    /// <param name="cancellationToken">Cancels the save. Kaleido passes <see cref="CancellationToken.None"/>: the answer of an attempt whose caller has gone must still be stored.</param>
    Task CompleteAsync(IdempotentResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an admitted key that did not complete with an answer to keep (its request failed),
    /// so the key may be used again. Removes only a record still in progress; a completed one stays.
    /// </summary>
    /// <param name="key">The scoped idempotency key.</param>
    /// <param name="cancellationToken">Cancels the removal. Kaleido passes <see cref="CancellationToken.None"/>.</param>
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds events that do not describe a save (plan built, execution completed, query executed) to
    /// the outbox.
    /// </summary>
    Task EnqueueEventsAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims up to <paramref name="maxCount"/> entries that are due (not parked,
    /// <see cref="KaleidoOutboxEvent.NextAttemptUtc"/> reached) and not leased by anyone, leases them
    /// to this instance until now + <paramref name="lease"/>, and returns exactly the entries it
    /// leased, oldest first.
    /// </summary>
    /// <remarks>
    /// Must be one atomic compare-and-set (a write transaction, a row lock, an
    /// <c>UPDATE ... WHERE lease expired</c>, a conditional put): two instances claiming at the same
    /// time never get the same entry. An entry whose lease expired (its instance stopped) can be
    /// claimed again - that is how another instance takes over.
    /// </remarks>
    Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a published entry - only while this instance still holds its lease (an instance whose
    /// lease expired must not remove an entry another instance has claimed since).
    /// </summary>
    Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed publish attempt and releases the lease - only while this instance still
    /// holds it: increments <see cref="KaleidoOutboxEvent.Attempts"/>, stores
    /// <paramref name="errorMessage"/>, and schedules the next attempt at <paramref name="retryAtUtc"/>; null
    /// parks the entry (kept, never claimed again).
    /// </summary>
    Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken = default);
}

/// <summary>
/// One event in the outbox: the serialized event envelope plus its delivery state.
/// </summary>
/// <remarks>
/// <see cref="EventType"/> (one of <see cref="KaleidoEventTypes"/>) tells the relay which event and
/// context types <see cref="Payload"/> holds; no CLR type names are stored. A custom store keeps
/// every member as is and treats <see cref="Payload"/> as opaque text (a step's request and
/// response are part of it, so allow several KB).
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record KaleidoOutboxEvent
{
    /// <summary>The event's <see cref="IKaleidoEvent.EventId"/>; consumers use it to drop duplicates.</summary>
    public required Guid EventId { get; init; }

    /// <summary>The event type (<see cref="KaleidoEventTypes"/>).</summary>
    public required string EventType { get; init; }

    /// <summary>The serialized <c>KaleidoEventEnvelope</c> (JSON).</summary>
    public required string Payload { get; init; }

    /// <summary>When the event occurred; entries are claimed oldest first.</summary>
    public required DateTimeOffset OccurredUtc { get; init; }

    /// <summary>Failed publish attempts so far.</summary>
    public int Attempts { get; init; }

    /// <summary>The earliest time the entry may be claimed (again).</summary>
    public DateTimeOffset NextAttemptUtc { get; init; }

    /// <summary>The error of the last failed attempt, if any.</summary>
    public string? LastError { get; init; }

    /// <summary>True when the entry gave up after the maximum attempts; kept, never claimed again.</summary>
    public bool IsParked { get; init; }
}

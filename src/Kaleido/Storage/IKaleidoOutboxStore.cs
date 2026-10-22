namespace Kaleido.Storage;

/// <summary>
/// The storage of Kaleido's built-in outbox: events waiting to be handed to
/// <see cref="IEventPublisher"/>, with their delivery state. In memory by default; durable with a
/// provider (e.g. <c>AddSqliteOutboxStore</c>) or your own (<c>AddOutboxStore&lt;T&gt;()</c>). Not used
/// when the host brings its own outbox (<see cref="IKaleidoOutbox"/>, <c>AddOutbox&lt;T&gt;()</c>) or
/// publishes directly (ADR 0023).
/// </summary>
/// <remarks>
/// <para>
/// <strong>At least once:</strong> the built-in outbox adds Kaleido's events here
/// (<see cref="AddAsync"/>) - a transition's events inside its transaction - and a background relay
/// claims each one and hands it to <see cref="IEventPublisher"/>, retrying until it succeeds, across
/// restarts when the storage is durable.
/// </para>
/// <para>
/// <strong>Atomic with the process state</strong> when both are on one database and
/// <see cref="IKaleidoTransactions"/> covers it (e.g. <c>AddSqliteContextStore</c> and
/// <c>AddSqliteOutboxStore</c> on one SQLite database): write in the open transaction when there is
/// one, on your own otherwise.
/// </para>
/// <para>
/// <strong>Several instances:</strong> with several instances on one shared store, each relay claims
/// entries (<see cref="ClaimEventsAsync"/>) before publishing them; a claim must be an atomic
/// compare-and-set, so an entry is never in two instances' hands at once.
/// </para>
/// </remarks>
public interface IKaleidoOutboxStore
{
    /// <summary>
    /// Adds events to the outbox, due now: within the current transition's transaction when one is
    /// open on this store's database, otherwise on their own.
    /// </summary>
    /// <param name="events">The entries to add.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken);

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
    Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a published entry - only while this instance still holds its lease (an instance whose
    /// lease expired must not remove an entry another instance has claimed since).
    /// </summary>
    Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a failed publish attempt and releases the lease - only while this instance still
    /// holds it: increments <see cref="KaleidoOutboxEvent.Attempts"/>, stores
    /// <paramref name="errorMessage"/>, and schedules the next attempt at <paramref name="retryAtUtc"/>; null
    /// parks the entry (kept, never claimed again).
    /// </summary>
    Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken);
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

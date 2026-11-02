namespace Kaleido.Storage;

/// <summary>
/// The storage of Kaleido's outbox: every event a service writes, waiting for the relay to hand it to
/// <see cref="IEventPublisher"/>, with its delivery state. In memory by default; durable with a provider
/// (e.g. <c>AddSqliteOutboxStore</c>) or your own (<c>AddOutboxStore&lt;T&gt;()</c>). Every service writes to
/// it; the relay (<c>AddOutboxRelay&lt;TPublisher&gt;()</c>) runs in one host on its database (ADR 0023, #552).
/// </summary>
/// <remarks>
/// <para>
/// <strong>At least once:</strong> every service adds its events here (<see cref="AddAsync"/>) - a
/// transition's events inside its transaction - and the relay claims each one and hands it to
/// <see cref="IEventPublisher"/>, retrying until it succeeds, across restarts when the storage is durable.
/// </para>
/// <para>
/// <strong>Shared by services (#552):</strong> several services, and versions of one, may write to one
/// outbox database; each entry names its <see cref="KaleidoOutboxEvent.ServiceName"/> and
/// <see cref="KaleidoOutboxEvent.ServiceVersion"/>. A relay drains everything in the database it reads:
/// <see cref="ClaimEventsAsync"/> claims any service's entries.
/// </para>
/// <para>
/// <strong>Atomic with the process state</strong> when both are on one database and
/// <see cref="IKaleidoTransactions"/> covers it (e.g. <c>AddSqliteContextStore</c> and
/// <c>AddSqliteOutboxStore</c> on one SQLite database): write in the open transaction when there is
/// one, on your own otherwise. Keep the outbox on the context store's database: an outbox on another
/// database writes its events on their own, and if the transition's commit then fails, they are still
/// relayed, describing a state that was never saved.
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

    /// <summary>
    /// Records that a relay instance is draining this outbox (its heartbeat): stores
    /// <paramref name="relay"/> under its <see cref="KaleidoOutboxRelay.InstanceId"/>, replacing that
    /// instance's earlier record. Called by the relay every <see cref="KaleidoEventOptions.RelayHeartbeatInterval"/>.
    /// </summary>
    /// <remarks>
    /// The writers' health check reads the latest heartbeat from the same database (<see cref="GetStatusAsync"/>),
    /// so a missing relay is seen without any call between services (#552). A store may remove the records of
    /// relays not seen for a long time (a stopped instance never records again; a restart is a new instance).
    /// </remarks>
    Task RecordRelayAsync(KaleidoOutboxRelay relay, CancellationToken cancellationToken);

    /// <summary>
    /// What the outbox health check needs, at <paramref name="now"/>: when a relay was last seen, how many
    /// entries are due (not parked, <see cref="KaleidoOutboxEvent.NextAttemptUtc"/> reached), and when the
    /// oldest due one occurred.
    /// </summary>
    Task<KaleidoOutboxStatus> GetStatusAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>
/// A relay instance's heartbeat in the outbox it drains (<see cref="IKaleidoOutboxStore.RecordRelayAsync"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record KaleidoOutboxRelay
{
    /// <summary>The relay host's instance (<see cref="KaleidoServiceOptions.InstanceId"/>); one record per instance.</summary>
    public required Guid InstanceId { get; init; }

    /// <summary>The relay host's service name (<see cref="KaleidoServiceOptions.ServiceName"/>), for operators.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The publisher it hands the entries to (its type name), for operators.</summary>
    public required string Publisher { get; init; }

    /// <summary>When the relay was seen (UTC).</summary>
    public required DateTimeOffset SeenUtc { get; init; }
}

/// <summary>
/// The outbox's state for its health check (<see cref="IKaleidoOutboxStore.GetStatusAsync"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record KaleidoOutboxStatus
{
    /// <summary>When any relay was last seen draining this outbox; <see langword="null"/> when none ever was.</summary>
    public DateTimeOffset? LastRelaySeenUtc { get; init; }

    /// <summary>The entries that are due: not parked, and their next attempt reached.</summary>
    public long DueCount { get; init; }

    /// <summary>When the oldest due entry occurred; <see langword="null"/> when none is due.</summary>
    public DateTimeOffset? OldestDueUtc { get; init; }
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

    /// <summary>
    /// The service that wrote the event (<see cref="KaleidoServiceOptions.ServiceName"/>): with several
    /// services on one outbox database, whose it is (#552). Set by Kaleido; a store keeps it as given.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// The version of <see cref="ServiceName"/> (<see cref="KaleidoServiceOptions.ServiceVersion"/>);
    /// <see langword="null"/> when it is unversioned. A store keeps it as given.
    /// </summary>
    public string? ServiceVersion { get; init; }

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

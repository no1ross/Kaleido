namespace Kaleido.Eventing;

/// <summary>
/// Publishes Kaleido domain events to your event infrastructure (a bus, a warehouse feed, an HTTP
/// collector). It belongs to the host that runs Kaleido's outbox relay
/// (<c>AddOutboxRelay&lt;TPublisher&gt;()</c>): every service only writes its events to the outbox, and the
/// relay hands them to this publisher (#552).
/// </summary>
/// <remarks>
/// <para>
/// Events are the complete immutable audit trail of everything that happens in Kaleido:
/// process creation, planning decisions (what steps were evaluated and why), every step
/// execution (request payload, response payload, decision type, outcome, business messages),
/// and every query executed (context, filters, results).
/// </para>
/// <para>
/// <strong>Privacy:</strong> event payloads are complete (a step's request and response, a query's
/// records), so an audit can show what was submitted and what a caller saw. Protect sensitive values
/// <em>before</em> they are written to the outbox, not in the publisher (the outbox stores the payload
/// until it is published): mark properties with your own <c>DataClassificationAttribute</c>
/// subclasses (e.g. <c>[PHI]</c>, <c>[PII]</c>) and choose a redactor per classification for events with
/// <c>AddEventRedaction</c> (separate from log redaction) - without it, classified
/// values are erased - and use
/// <see cref="IKaleidoEventRedactor"/> for anything attributes cannot express.
/// </para>
/// <para>
/// The process state stored in the database contains only what is required to resume
/// execution — it is not a history. The events are: without a relay publishing them there is no data
/// warehouse feed, audit trail, analytics or replay, and the outbox keeps growing.
/// </para>
/// <para>
/// <strong>At least once, never on the request path:</strong> every service writes every event to its
/// outbox (<see cref="Storage.IKaleidoOutboxStore"/>; process events that describe a save in the
/// same transaction as the state), and the relay - in that service or in another host on the same
/// database - claims the entries and calls <see cref="PublishAsync"/>, awaiting it. A call that throws or times out is retried later with backoff; after the configured maximum
/// attempts the event is parked (kept, logged at Error), never dropped. A publisher never slows down
/// or fails a request. Kaleido guarantees the hand-off to this publisher, not the receipt by the
/// final receiver - that, and any broker-side retries, are owned by the implementation.
/// </para>
/// <para>
/// <strong>Duplicates and order:</strong> after a crash between a successful publish and its
/// bookkeeping the same event is published again; use <see cref="IKaleidoEvent.EventId"/> to drop
/// duplicates. There is no ordering guarantee: batches are published in parallel and retries reorder
/// events. Order a process's events by <see cref="ProcessEventContext.ProcessVersion"/>.
/// </para>
/// <para>
/// <strong>Payload shape:</strong> the relay publishes the stored (JSON) form of the envelope, so
/// <see cref="object"/>-typed members (a step's request and response, query records) arrive as
/// <see cref="System.Text.Json.JsonElement"/>. The publisher is a singleton called outside any
/// request scope; it must not depend on request-scoped services.
/// </para>
/// <para>
/// Register it with the relay, on the host that drains the outbox (a service, or a host with nothing
/// else):
/// <code>
/// builder.Services.AddKaleido(builder.Configuration, new() { ServiceName = "outbox-relay", /* ... */ })
///     .AddSqliteOutboxStore(connectionString)   // the database the services write their events to
///     .AddOutboxRelay&lt;MyEventPublisher&gt;();
/// </code>
/// </para>
/// </remarks>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a domain event wrapped in its correlation and traceability context envelope.
    /// </summary>
    Task PublishAsync<TEvent, TContext>(
        KaleidoEventEnvelope<TEvent, TContext> envelope,
        CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent;
}

/// <summary>A Kaleido domain event: identified, and stamped with when it occurred.</summary>
public interface IKaleidoEvent
{
    /// <summary>
    /// Uniquely identifies this event. Events are handed to <see cref="IEventPublisher"/> at least
    /// once: after a crash between a successful publish and its bookkeeping the same event is
    /// published again with the same id, so consumers use it to drop duplicates.
    /// </summary>
    Guid EventId { get; }

    /// <summary>When the event occurred (UTC).</summary>
    DateTimeOffset OccurredOn { get; }
}

using Microsoft.Extensions.Logging;

namespace Kaleido.Eventing;

/// <summary>
/// Publishes Kaleido domain events to any registered event infrastructure.
/// The default implementation is <see cref="EventPublisher"/>, which discards all events.
/// Replace with a real implementation before calling <c>AddKaleido()</c>.
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
/// subclasses (e.g. <c>[PHI]</c>, <c>[PII]</c>) and choose a redactor per classification with
/// <c>AddRedaction</c> - without one, classified values are erased - and use
/// <see cref="IKaleidoEventRedactor"/> for anything attributes cannot express.
/// </para>
/// <para>
/// The process state stored in the database contains only what is required to resume
/// execution — it is not a history. Without a real <see cref="IEventPublisher"/>,
/// you will have no data warehouse feed, no audit trail, no analytics, and no replay capability.
/// </para>
/// <para>
/// <strong>At least once, never on the request path:</strong> Kaleido writes every event to the
/// outbox of <see cref="Storage.IKaleidoContextStore"/> (process events that describe a save in the
/// same transaction as the state) and a background relay calls <see cref="PublishAsync"/>, awaiting
/// it. A call that throws or times out is retried later with backoff; after the configured maximum
/// attempts the event is parked (kept, logged at Error), never dropped. A publisher never slows down
/// or fails a request. Kaleido guarantees the hand-off to this publisher, not the receipt by the
/// final receiver - that, and any broker-side retries, are owned by the implementation.
/// </para>
/// <para>
/// <strong>Duplicates and order:</strong> after a crash between a successful publish and its
/// bookkeeping the same event is published again; use <see cref="IKaleidoEvent.EventId"/> to drop
/// duplicates. Retries can reorder events; use <see cref="IKaleidoEvent.OccurredOn"/>.
/// </para>
/// <para>
/// <strong>Payload shape:</strong> the relay publishes the stored (JSON) form of the envelope, so
/// <see cref="object"/>-typed members (a step's request and response, query records) arrive as
/// <see cref="System.Text.Json.JsonElement"/>. The publisher is a singleton called outside any
/// request scope; it must not depend on request-scoped services.
/// </para>
/// <para>
/// Register a real publisher before calling <c>AddKaleido()</c>, or use
/// <c>AddEventPublisher&lt;TPublisher&gt;()</c> on the builder:
/// <code>
/// builder.Services.AddKaleido(builder.Configuration)
///     .AddEventPublisher&lt;MyEventPublisher&gt;();
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
        CancellationToken cancellationToken = default)
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

// Marks a publisher that discards every event (the default when none is registered): Kaleido then
// writes nothing to the outbox and the relay stays idle.
internal interface INoOpEventPublisher : IEventPublisher;

/// <summary>
/// Default no-op event publisher registered by <c>AddKaleido()</c>.
/// Discards all events and logs a warning at startup. Replace with a real
/// implementation — see <see cref="IEventPublisher"/> remarks.
/// </summary>
internal sealed partial class EventPublisher : INoOpEventPublisher
{
    public EventPublisher(ILogger<EventPublisher> logger)
    {
        LogNoPublisher(logger);
    }

    public Task PublishAsync<TEvent, TContext>(
        KaleidoEventEnvelope<TEvent, TContext> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent
        => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "No IEventPublisher has been registered. Kaleido events are being silently discarded. Events carry the complete audit trail of process and query activity — without a real publisher you will have no data warehouse feed, audit trail, analytics, or replay capability. Call AddEventPublisher<TPublisher>() on the IKaleidoBuilder to register a real implementation.")]
    private static partial void LogNoPublisher(ILogger logger);
}

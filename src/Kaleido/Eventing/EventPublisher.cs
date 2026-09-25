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
/// The process state stored in the database contains only what is required to resume
/// execution — it is not a history. Without a real <see cref="IEventPublisher"/>,
/// you will have no data warehouse feed, no audit trail, no analytics, and no replay capability.
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

/// <summary>Marker interface for all Kaleido domain events.</summary>
public interface IKaleidoEvent
{
    DateTimeOffset OccurredOn { get; }
}

/// <summary>
/// Default no-op event publisher registered by <c>AddKaleido()</c>.
/// Discards all events and logs a warning at startup. Replace with a real
/// implementation — see <see cref="IEventPublisher"/> remarks.
/// </summary>
public sealed class EventPublisher : IEventPublisher
{
    public EventPublisher(ILogger<EventPublisher> logger)
    {
        logger.LogWarning(
            "No IEventPublisher has been registered. Kaleido events are being silently discarded. " +
            "Events carry the complete audit trail of process and query activity — " +
            "without a real publisher you will have no data warehouse feed, audit trail, analytics, or replay capability. " +
            "Call AddEventPublisher<TPublisher>() on the IKaleidoBuilder to register a real implementation.");
    }

    public Task PublishAsync<TEvent, TContext>(
        KaleidoEventEnvelope<TEvent, TContext> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IKaleidoEvent
        => Task.CompletedTask;
}

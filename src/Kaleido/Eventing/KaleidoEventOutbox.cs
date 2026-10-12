using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Kaleido.Eventing;

// How the runtime writes events: as outbox entries that are saved together with the state, or
// enqueued on their own. Without a registered publisher nothing is written, so the outbox does not
// grow when nobody listens.
internal interface IKaleidoEventOutbox
{
    // The outbox entry for a save, or null when no publisher is registered.
    KaleidoOutboxEvent? Prepare<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent;

    // Writes an event that is not part of a save. Never fails the request: a store error is logged
    // and counted. Callers pass CancellationToken.None: the work the event describes is already done,
    // so a cancelled request must not drop its event.
    Task EnqueueAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent;

    // Wakes the relay after events were saved with the state.
    void Saved();
}

internal sealed partial class KaleidoEventOutbox(
    IKaleidoContextStore store,
    IEventPublisher publisher,
    IKaleidoEventSignal signal,
    ILogger<KaleidoEventOutbox> logger)
    : IKaleidoEventOutbox
{
    private static readonly Meter Meter = new(KaleidoEventTelemetry.MeterName);

    private static readonly Counter<long> EnqueueFailures =
        Meter.CreateCounter<long>(
            KaleidoEventTelemetry.EnqueueFailuresCounterName,
            description: "Events that could not be written to the outbox.");

    private bool Enabled => publisher is not INoOpEventPublisher;

    public KaleidoOutboxEvent? Prepare<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent =>
        Enabled ? envelope.ToOutboxEvent() : null;

    public async Task EnqueueAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            await store.EnqueueEventsAsync([envelope.ToOutboxEvent()], cancellationToken);
            signal.Notify();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            EnqueueFailures.Add(1, new KeyValuePair<string, object?>(KaleidoEventTelemetry.TagEventType, envelope.EventType));
            LogEnqueueFailed(logger, exception, envelope.EventType, envelope.Event.EventId);
        }
    }

    public void Saved() => signal.Notify();

    [LoggerMessage(Level = LogLevel.Error, Message = "Event {EventType} ({EventId}) could not be written to the outbox and is lost. The request was not affected.")]
    private static partial void LogEnqueueFailed(ILogger logger, Exception exception, string eventType, Guid eventId);
}

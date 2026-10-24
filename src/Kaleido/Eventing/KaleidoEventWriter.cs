using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Kaleido.Eventing;

// How the runtime produces its events and hands the ones outside a transition to the configured
// outbox integration (IKaleidoOutbox); a transition's event is added by ProcessorTransition inside the
// transaction. Kaleido decides when an event exists; the integration makes it durable and delivers it. With the built-in outbox or direct publication and no registered publisher
// no event is produced, so nothing grows when nobody listens.
internal interface IKaleidoEventWriter
{
    // The outbox entry of a transition's event, or null when events are off.
    KaleidoOutboxEvent? Prepare<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent;

    // After a transition committed: lets the outbox deliver. Never fails (the state is committed).
    Task CommittedAsync(CancellationToken cancellationToken);

    // A transition with an event did not commit: the outbox forgets the event (#451).
    void RolledBack();

    // Writes an event that is not part of a transition. Never fails the request: an outbox error is
    // logged and counted. Callers pass CancellationToken.None: the work the event describes is already
    // done, so a cancelled request must not drop its event.
    Task WriteAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent;
}

internal sealed partial class KaleidoEventWriter(
    IKaleidoOutbox outbox,
    IEventPublisher publisher,
    IKaleidoEventPayloadWriter payloadWriter,
    ILogger<KaleidoEventWriter> logger)
    : IKaleidoEventWriter
{
    private static readonly Meter Meter = new(KaleidoEventTelemetry.MeterName);

    private static readonly Counter<long> EnqueueFailures =
        Meter.CreateCounter<long>(
            KaleidoEventTelemetry.EnqueueFailuresCounterName,
            description: "Events that could not be written to the outbox.");

    private bool Enabled => !(outbox is IKaleidoPublisherOutbox && publisher is INoOpEventPublisher);

    public KaleidoOutboxEvent? Prepare<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent =>
        Enabled ? payloadWriter.Write(envelope) : null;

    public async Task CommittedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await outbox.CommittedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommittedFailed(logger, exception);
        }
    }

    // The host's own outbox writes inside its transaction, so the rollback is its own; only Kaleido's
    // publisher-backed integrations are told.
    public void RolledBack()
    {
        if (outbox is IKaleidoPublisherOutbox publisherOutbox)
        {
            publisherOutbox.Discard();
        }
    }

    public async Task WriteAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            await outbox.AddAsync([payloadWriter.Write(envelope)], cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            EnqueueFailures.Add(1, new KeyValuePair<string, object?>(KaleidoEventTelemetry.TagEventType, envelope.EventType));
            LogEnqueueFailed(logger, exception, envelope.EventType, envelope.Event.EventId);
            return;
        }

        await CommittedAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Event {EventType} ({EventId}) could not be written to the outbox and is lost. The request was not affected.")]
    private static partial void LogEnqueueFailed(ILogger logger, Exception exception, string eventType, Guid eventId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The outbox failed after events were committed. The events are stored; the request was not affected.")]
    private static partial void LogCommittedFailed(ILogger logger, Exception exception);
}

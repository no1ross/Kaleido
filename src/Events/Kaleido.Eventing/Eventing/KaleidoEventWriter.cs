using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Kaleido.Eventing;

// How the runtime produces its events and hands the ones outside a transition to the outbox
// (IKaleidoOutbox); a transition's event is added by ProcessorTransition inside the transaction.
// Every service writes every event (#552): publishing is the relay's (AddOutboxRelay), in this host or
// in another on the same database.
internal interface IKaleidoEventWriter
{
    // The outbox entry of a transition's event.
    KaleidoOutboxEvent Prepare<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent;

    // After a transition committed: wakes this host's relay. Never fails (the state is committed).
    Task CommittedAsync(CancellationToken cancellationToken);

    // Writes an event that is not part of a transition. Never fails the request: an outbox error is
    // logged and counted. Callers pass CancellationToken.None: the work the event describes is already
    // done, so a cancelled request must not drop its event.
    Task WriteAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent;
}

internal sealed partial class KaleidoEventWriter(
    IKaleidoOutbox outbox,
    IKaleidoEventPayloadWriter payloadWriter,
    ILogger<KaleidoEventWriter> logger)
    : IKaleidoEventWriter
{
    private static readonly Meter Meter = new(KaleidoEventTelemetry.MeterName);

    private static readonly Counter<long> EnqueueFailures =
        Meter.CreateCounter<long>(
            KaleidoEventTelemetry.EnqueueFailuresCounterName,
            description: "Events that could not be written to the outbox.");

    public KaleidoOutboxEvent Prepare<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
        where TEvent : IKaleidoEvent =>
        payloadWriter.Write(envelope);

    public async Task CommittedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await outbox.CommittedAsync(cancellationToken);
        }
        // Only the caller's cancellation is passed on; any other failure - an OperationCanceledException of
        // the outbox's own (a timeout) included - is logged: this never fails (#517).
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogCommittedFailed(logger, exception);
        }
    }

    public async Task WriteAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent
    {
        try
        {
            await outbox.AddAsync([payloadWriter.Write(envelope)], cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
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

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Kaleido.Eventing;

// AddDirectPublication(): no outbox. A request's events are held until they are committed, then
// published once through the publisher - never before the state they describe is saved. Weaker by
// choice: an event is lost when publishing fails or the process stops first.
internal sealed partial class KaleidoDirectPublicationOutbox(
    IEventPublisher publisher,
    ILogger<KaleidoDirectPublicationOutbox> logger)
    : IKaleidoPublisherOutbox
{
    private static readonly Meter Meter = new(KaleidoEventTelemetry.MeterName);
    private static readonly Counter<long> Published = Meter.CreateCounter<long>(KaleidoEventTelemetry.PublishedCounterName, description: "Events handed to the publisher.");
    private static readonly Counter<long> Lost = Meter.CreateCounter<long>(KaleidoEventTelemetry.FailedCounterName, description: "Failed publish attempts.");

    private readonly List<KaleidoOutboxEvent> _pending = [];

    public Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        cancellationToken.ThrowIfCancellationRequested();

        _pending.AddRange(events);
        return Task.CompletedTask;
    }

    // The transition rolled back: its events describe a state that was never saved (#451).
    public void Discard() => _pending.Clear();

    public async Task CommittedAsync(CancellationToken cancellationToken)
    {
        KaleidoOutboxEvent[] committed = [.. _pending];
        _pending.Clear();

        foreach (var entry in committed)
        {
            var tag = new KeyValuePair<string, object?>(KaleidoEventTelemetry.TagEventType, entry.EventType);

            // The publisher is foreign code (it may throw anything); the state is already committed,
            // so a failure loses this event, not the request.
            try
            {
                await publisher.PublishOutboxEventAsync(entry, cancellationToken);
                Published.Add(1, tag);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Lost.Add(1, tag);
                LogPublishFailed(logger, exception, entry.EventType, entry.EventId);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Event {EventType} ({EventId}) could not be published and is lost: direct publication has no outbox to retry from. The request was not affected.")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, string eventType, Guid eventId);
}

using Kaleido.Storage;

namespace Kaleido.Provider.SQLite.Entities;

// The OutboxEvents row of an outbox entry and back (times as UTC ticks). Shared by the context store,
// which adds a save's events in the state's transaction, and the outbox store.
internal static class OutboxEventEntityExtensions
{
    public static OutboxEventEntity ToEntity(this KaleidoOutboxEvent @event) =>
        new()
        {
            EventId = @event.EventId,
            EventType = @event.EventType,
            Payload = @event.Payload,
            OccurredUtc = @event.OccurredUtc.UtcTicks,
            Attempts = @event.Attempts,
            NextAttemptUtc = @event.NextAttemptUtc.UtcTicks,
            LastError = @event.LastError,
            IsParked = @event.IsParked,
        };

    public static KaleidoOutboxEvent ToOutboxEvent(this OutboxEventEntity entity) =>
        new()
        {
            EventId = entity.EventId,
            EventType = entity.EventType,
            Payload = entity.Payload,
            OccurredUtc = new DateTimeOffset(entity.OccurredUtc, TimeSpan.Zero),
            Attempts = entity.Attempts,
            NextAttemptUtc = new DateTimeOffset(entity.NextAttemptUtc, TimeSpan.Zero),
            LastError = entity.LastError,
            IsParked = entity.IsParked,
        };
}

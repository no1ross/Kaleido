using Kaleido.Storage;

namespace Kaleido.Queryable.Eventing;

internal sealed class QueryableOutboxEventDispatcher : IOutboxEventDispatcher
{
    public bool CanHandle(string eventType) => string.Equals(eventType, QueryableEventTypes.QueryExecuted, StringComparison.Ordinal);

    public Task PublishAsync(IEventPublisher publisher, KaleidoOutboxEvent entry, CancellationToken cancellationToken) =>
        string.Equals(entry.EventType, QueryableEventTypes.QueryExecuted, StringComparison.Ordinal)
            ? publisher.PublishOutboxEventAsync<QueryExecuted, QueryableEventContext>(entry, cancellationToken)
            : throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Outbox event '{entry.EventId}' has the unknown event type '{entry.EventType}'.");
}

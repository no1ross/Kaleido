using Microsoft.Extensions.Logging;

namespace Kaleido.Storage;

// The default storage of the built-in outbox: in memory. It survives a publisher outage, not a
// restart; each instance relays only the events it wrote itself.
internal sealed partial class InMemoryKaleidoOutboxStore : IKaleidoOutboxStore
{
    private readonly Dictionary<Guid, OutboxEntry> _outbox = [];
    private readonly object _lock = new();
    private readonly TimeProvider _time;

    public InMemoryKaleidoOutboxStore(TimeProvider time, ILogger<InMemoryKaleidoOutboxStore> logger)
    {
        _time = time;
        LogInMemoryStoreActive(logger);
    }

    public Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            foreach (var @event in events)
            {
                _outbox[@event.EventId] = new OutboxEntry(@event);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = _time.GetUtcNow();

        lock (_lock)
        {
            var claimed = _outbox.Values
                .Where(x => !x.Event.IsParked && x.Event.NextAttemptUtc <= now && x.LeaseUntil <= now)
                .OrderBy(x => x.Event.OccurredUtc)
                .Take(maxCount)
                .ToList();

            foreach (var entry in claimed)
            {
                entry.LeaseUntil = now + lease;
            }

            return Task.FromResult<IReadOnlyList<KaleidoOutboxEvent>>([.. claimed.Select(x => x.Event)]);
        }
    }

    public Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (TryGetLeased(eventId, out _))
            {
                _outbox.Remove(eventId);
            }
        }

        return Task.CompletedTask;
    }

    public Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (TryGetLeased(eventId, out var entry))
            {
                entry.Event = entry.Event with
                {
                    Attempts = entry.Event.Attempts + 1,
                    LastError = errorMessage,
                    NextAttemptUtc = retryAtUtc ?? entry.Event.NextAttemptUtc,
                    IsParked = retryAtUtc is null,
                };
                entry.LeaseUntil = DateTimeOffset.MinValue;
            }
        }

        return Task.CompletedTask;
    }

    // A completed or failed attempt counts only while the lease is still held.
    private bool TryGetLeased(Guid eventId, [NotNullWhen(true)] out OutboxEntry? entry) =>
        _outbox.TryGetValue(eventId, out entry) && entry.LeaseUntil > _time.GetUtcNow();

    private sealed class OutboxEntry(KaleidoOutboxEvent @event)
    {
        public KaleidoOutboxEvent Event { get; set; } = @event;

        public DateTimeOffset LeaseUntil { get; set; } = DateTimeOffset.MinValue;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "InMemoryKaleidoOutboxStore is active. Events waiting to be published are kept in memory only: lost on restart, and each instance relays only its own. Register a durable outbox store on the database of the process state (e.g. AddSqliteOutboxStore, or AddOutboxStore<T>()), or your own outbox (AddOutbox<T>()), before deploying to production.")]
    private static partial void LogInMemoryStoreActive(ILogger logger);
}

using System.Collections.Concurrent;
using Kaleido.Processor.Context;
using Microsoft.Extensions.Logging;

namespace Kaleido.Storage;

// The default store: everything in memory, for development and tests. The outbox survives a
// publisher outage, not a restart; each instance relays only the events it wrote itself.
internal sealed partial class InMemoryKaleidoContextStore : IKaleidoContextStore
{
    private readonly ILogger<InMemoryKaleidoContextStore> _logger;
    private readonly ConcurrentDictionary<Guid, ProcessorContext> _contexts = new();
    private readonly ConcurrentDictionary<string, IdempotentResult> _results = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, OutboxEntry> _outbox = [];
    private readonly object _lock = new();

    public InMemoryKaleidoContextStore(
        ILogger<InMemoryKaleidoContextStore> logger)
    {
        _logger = logger;

        LogInMemoryStoreActive(logger);
    }

    public Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _contexts.TryGetValue(processId, out var context);
        return Task.FromResult(context);
    }

    public Task<ProcessorContext> SaveAsync(
        ProcessorContext context,
        IReadOnlyCollection<KaleidoOutboxEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(events);

        cancellationToken.ThrowIfCancellationRequested();

        ProcessorContext saved;

        // Compare and write as one step: two saves for the same process must not both pass the
        // check, and the events are added only together with the state.
        lock (_lock)
        {
            var storedVersion = _contexts.TryGetValue(context.ProcessId, out var stored) ? stored.Version : 0;

            if (storedVersion != context.Version)
            {
                throw new KaleidoConcurrencyException(context.ProcessId);
            }

            saved = context with { Version = context.Version + 1 };
            _contexts[context.ProcessId] = saved;
            AddEvents(events);
        }

        LogSaved(_logger, context.ProcessId, context.Steps.Count);

        return Task.FromResult(saved);
    }

    public Task<IdempotentResult?> FindResultAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            _results.TryGetValue(key, out var result) && result.ExpiresUtc > DateTimeOffset.UtcNow
                ? result
                : null);
    }

    public Task SaveResultAsync(IdempotentResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        cancellationToken.ThrowIfCancellationRequested();

        _results[result.Key] = result;

        // Expired answers are dropped as new ones arrive, so the dictionary does not grow without bound.
        var now = DateTimeOffset.UtcNow;

        foreach (var expired in _results.Where(x => x.Value.ExpiresUtc <= now).Select(x => x.Key).ToList())
        {
            _results.TryRemove(expired, out _);
        }

        return Task.CompletedTask;
    }

    public Task EnqueueEventsAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            AddEvents(events);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;

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

    public Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
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

    public Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken = default)
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

    private void AddEvents(IReadOnlyCollection<KaleidoOutboxEvent> events)
    {
        foreach (var @event in events)
        {
            _outbox[@event.EventId] = new OutboxEntry(@event);
        }
    }

    // A completed or failed attempt counts only while the lease is still held.
    private bool TryGetLeased(Guid eventId, [NotNullWhen(true)] out OutboxEntry? entry) =>
        _outbox.TryGetValue(eventId, out entry) && entry.LeaseUntil > DateTimeOffset.UtcNow;

    private sealed class OutboxEntry(KaleidoOutboxEvent @event)
    {
        public KaleidoOutboxEvent Event { get; set; } = @event;

        public DateTimeOffset LeaseUntil { get; set; } = DateTimeOffset.MinValue;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "InMemoryKaleidoContextStore is active. Process state, idempotent results and outbox events are kept in memory only: lost on restart, and process state grows without bound in long-running processes. Register a durable IKaleidoContextStore (e.g. UseSqliteKaleidoContextStore) before deploying to production.")]
    private static partial void LogInMemoryStoreActive(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process context saved for process {ProcessId} ({StepCount} steps).")]
    private static partial void LogSaved(ILogger logger, Guid processId, int stepCount);
}

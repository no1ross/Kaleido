using System.Collections.Concurrent;
using Kaleido.Processor.Context;
using Microsoft.Extensions.Logging;

namespace Kaleido.Storage;

// The default process-state store: in memory, for development and tests. A save's events go to the
// registered outbox right after the state (the in-memory outbox enqueues synchronously and cannot
// fail, so they stay together, as the durable stores keep them in the state's transaction).
internal sealed partial class InMemoryKaleidoContextStore : IKaleidoContextStore
{
    private readonly ILogger<InMemoryKaleidoContextStore> _logger;
    private readonly IKaleidoOutboxStore _outbox;
    private readonly ConcurrentDictionary<Guid, ProcessorContext> _contexts = new();
    private readonly object _lock = new();

    public InMemoryKaleidoContextStore(
        ILogger<InMemoryKaleidoContextStore> logger,
        IKaleidoOutboxStore outbox)
    {
        _logger = logger;
        _outbox = outbox;

        LogInMemoryStoreActive(logger);
    }

    public Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _contexts.TryGetValue(processId, out var context);
        return Task.FromResult(context);
    }

    public async Task<ProcessorContext> SaveAsync(
        ProcessorContext context,
        IReadOnlyCollection<KaleidoOutboxEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(events);

        cancellationToken.ThrowIfCancellationRequested();

        ProcessorContext saved;

        // Compare and write as one step: two saves for the same process must not both pass the check.
        lock (_lock)
        {
            var storedVersion = _contexts.TryGetValue(context.ProcessId, out var stored) ? stored.Version : 0;

            if (storedVersion != context.Version)
            {
                throw new KaleidoConcurrencyException(context.ProcessId);
            }

            saved = context with { Version = context.Version + 1 };
            _contexts[context.ProcessId] = saved;
        }

        await _outbox.EnqueueEventsAsync(events, CancellationToken.None);

        LogSaved(_logger, context.ProcessId, context.Steps.Count);

        return saved;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "InMemoryKaleidoContextStore is active. Process state is kept in memory only: lost on restart, and it grows without bound in long-running processes. Register a durable IKaleidoContextStore (e.g. UseSqliteKaleidoContextStore) before deploying to production.")]
    private static partial void LogInMemoryStoreActive(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process context saved for process {ProcessId} ({StepCount} steps).")]
    private static partial void LogSaved(ILogger logger, Guid processId, int stepCount);
}

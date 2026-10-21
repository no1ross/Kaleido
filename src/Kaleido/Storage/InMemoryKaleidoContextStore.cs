using System.Collections.Concurrent;
using Kaleido.Processor.Context;
using Microsoft.Extensions.Logging;

namespace Kaleido.Storage;

// The default process-state store: in memory, for development and tests. State only: a transition's
// events are added by the outbox integration (no transaction in memory; neither write can fail).
internal sealed partial class InMemoryKaleidoContextStore : IKaleidoContextStore
{
    private readonly ILogger<InMemoryKaleidoContextStore> _logger;
    private readonly ConcurrentDictionary<Guid, ProcessorContext> _contexts = new();
    private readonly object _lock = new();

    public InMemoryKaleidoContextStore(ILogger<InMemoryKaleidoContextStore> logger)
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

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

        LogSaved(_logger, context.ProcessId, context.Steps.Count);

        return Task.FromResult(saved);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "InMemoryKaleidoContextStore is active. Process state is kept in memory only: lost on restart, and it grows without bound in long-running processes. Register a durable IKaleidoContextStore (e.g. AddSqliteContextStore, or AddContextStore<T>()) before deploying to production.")]
    private static partial void LogInMemoryStoreActive(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process context saved for process {ProcessId} ({StepCount} steps).")]
    private static partial void LogSaved(ILogger logger, Guid processId, int stepCount);
}

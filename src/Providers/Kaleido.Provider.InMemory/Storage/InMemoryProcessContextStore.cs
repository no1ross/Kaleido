using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kaleido.Provider.InMemory.Storage;

// Explicit in-memory process-state storage for development and tests. State only: a transition's
// events are added by the outbox integration (no transaction in memory; neither write can fail). Each
// process is kept under its key - processor, version and id (#552).
internal sealed partial class InMemoryProcessContextStore : IProcessContextStore
{
    private readonly ILogger<InMemoryProcessContextStore> _logger;
    private readonly ConcurrentDictionary<ProcessKey, ProcessContext> _contexts = new();
    private readonly object _lock = new();

    public InMemoryProcessContextStore(ILogger<InMemoryProcessContextStore> logger)
    {
        _logger = logger;

        LogInMemoryStoreActive(logger);
    }

    public Task<ProcessContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _contexts.TryGetValue(key, out var context);
        return Task.FromResult(context);
    }

    public Task<ProcessContext> SaveAsync(
        ProcessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        ProcessContext saved;

        // Compare and write as one step: two saves for the same process must not both pass the check.
        lock (_lock)
        {
            var storedVersion = _contexts.TryGetValue(context.Key, out var stored) ? stored.Version : 0;

            if (storedVersion != context.Version)
            {
                throw new ProcessConcurrencyException(context.ProcessId);
            }

            saved = context with { Version = context.Version + 1 };
            _contexts[context.Key] = saved;
        }

        LogSaved(_logger, context.ProcessId, context.Steps.Count);

        return Task.FromResult(saved);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "InMemoryProcessContextStore is active. Process state is kept in memory only: lost on restart, and it grows without bound in long-running processes. Register a durable IProcessContextStore (e.g. AddSqliteContextProvider, or AddContextStore<T>()) before deploying to production.")]
    private static partial void LogInMemoryStoreActive(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process context saved for process {ProcessId} ({StepCount} steps).")]
    private static partial void LogSaved(ILogger logger, Guid processId, int stepCount);
}

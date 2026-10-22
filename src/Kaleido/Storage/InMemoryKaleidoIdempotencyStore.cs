using Kaleido.Processor.Context;
using Microsoft.Extensions.Logging;

namespace Kaleido.Storage;

// The default idempotency store: the records of requests sent with an idempotency key, in memory.
internal sealed partial class InMemoryKaleidoIdempotencyStore : IKaleidoIdempotencyStore
{
    private readonly Dictionary<string, IdempotentResult> _results = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public InMemoryKaleidoIdempotencyStore(ILogger<InMemoryKaleidoIdempotencyStore> logger) =>
        LogInMemoryStoreActive(logger);

    public Task<IdempotentResult?> AdmitAsync(IdempotentResult admission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admission);

        cancellationToken.ThrowIfCancellationRequested();

        // Check and insert as one step: two admissions of the same key must not both win.
        lock (_lock)
        {
            // Expired records are dropped as new keys arrive, so the dictionary does not grow without bound.
            var now = DateTimeOffset.UtcNow;

            foreach (var expired in _results.Where(x => x.Value.ExpiresUtc <= now).Select(x => x.Key).ToList())
            {
                _results.Remove(expired);
            }

            if (_results.TryGetValue(admission.Key, out var existing))
            {
                return Task.FromResult<IdempotentResult?>(existing);
            }

            _results[admission.Key] = admission;

            return Task.FromResult<IdempotentResult?>(null);
        }
    }

    public Task CompleteAsync(IdempotentResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _results[result.Key] = result;
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (_results.TryGetValue(key, out var existing) && existing.State == IdempotentResultState.InProgress)
            {
                _results.Remove(key);
            }
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "InMemoryKaleidoIdempotencyStore is active. The records of requests sent with an idempotency key are kept in memory only: lost on restart, so a retry after a restart runs its step again, and each instance knows only its own. Register a durable IKaleidoIdempotencyStore (e.g. UseSqliteKaleidoContextStore) before deploying to production.")]
    private static partial void LogInMemoryStoreActive(ILogger logger);
}

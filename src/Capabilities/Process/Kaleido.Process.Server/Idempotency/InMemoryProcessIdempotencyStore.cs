using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Server.Idempotency;

// Explicit in-memory idempotency storage for requests sent with an idempotency key.
internal sealed partial class InMemoryProcessIdempotencyStore : IProcessIdempotencyStore
{
    private readonly Dictionary<string, IdempotencyRecord> _results = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private readonly TimeProvider _time;

    public InMemoryProcessIdempotencyStore(TimeProvider time, ILogger<InMemoryProcessIdempotencyStore> logger)
    {
        _time = time;
        LogInMemoryStoreActive(logger);
    }

    public Task<IdempotencyRecord?> AdmitAsync(IdempotencyRecord admission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admission);

        cancellationToken.ThrowIfCancellationRequested();
        if (admission.State != IdempotencyState.InProgress || admission.Replay is not null)
        {
            throw new ArgumentException("An admission must be in progress without a replay.", nameof(admission));
        }

        // Check and insert as one step: two admissions of the same key must not both win.
        lock (_lock)
        {
            // Expired records are dropped as new keys arrive, so the dictionary does not grow without bound.
            var now = _time.GetUtcNow();

            foreach (var expired in _results.Where(x => x.Value.ExpiresUtc <= now).Select(x => x.Key).ToList())
            {
                _results.Remove(expired);
            }

            if (_results.TryGetValue(admission.Key, out var existing))
            {
                return Task.FromResult<IdempotencyRecord?>(existing);
            }

            _results[admission.Key] = admission;

            return Task.FromResult<IdempotencyRecord?>(null);
        }
    }

    public Task<bool> CompleteAsync(IdempotencyRecord result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        cancellationToken.ThrowIfCancellationRequested();
        if (result.State is not (IdempotencyState.Completed or IdempotencyState.OutcomeUnknown)
            || (result.State == IdempotencyState.Completed) != (result.Replay is not null))
        {
            throw new ArgumentException("A completed result requires a replay; an unknown outcome has none.", nameof(result));
        }

        // Compare and set: only the admission that holds the key, while it is in progress.
        lock (_lock)
        {
            if (!HoldsTheKey(result.Key, result.AdmissionId))
            {
                return Task.FromResult(false);
            }

            _results[result.Key] = result;
        }

        return Task.FromResult(true);
    }

    public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult(HoldsTheKey(key, admissionId) && _results.Remove(key));
        }
    }

    // Called under the lock.
    private bool HoldsTheKey(string key, Guid admissionId) =>
        _results.TryGetValue(key, out var existing)
        && existing.AdmissionId == admissionId
        && existing.State == IdempotencyState.InProgress;

    [LoggerMessage(Level = LogLevel.Warning, Message = "InMemoryProcessIdempotencyStore is active. The records of requests sent with an idempotency key are kept in memory only: lost on restart, so a retry after a restart runs its step again, and each instance knows only its own. Register a durable IProcessIdempotencyStore before deploying to production.")]
    private static partial void LogInMemoryStoreActive(ILogger logger);
}

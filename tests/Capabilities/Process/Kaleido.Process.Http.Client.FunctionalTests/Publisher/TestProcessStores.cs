namespace Kaleido.Process.Http.Client.FunctionalTests.Publisher;

public sealed class StoredProcesses
{
    public Dictionary<ProcessKey, ProcessContext> States { get; } = [];
    public object Gate { get; } = new();
}

public sealed class TestProcessContextStore(StoredProcesses state) : IProcessContextStore
{
    public Task<ProcessContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken)
    {
        lock (state.Gate)
        {
            return Task.FromResult(state.States.GetValueOrDefault(key));
        }
    }

    public Task<ProcessContext> SaveAsync(ProcessContext context, CancellationToken cancellationToken)
    {
        lock (state.Gate)
        {
            var stored = state.States.GetValueOrDefault(context.Key);
            if ((stored?.Version ?? 0) != context.Version)
            {
                throw new ProcessConcurrencyException(context.ProcessId);
            }

            var saved = context with { Version = context.Version + 1 };
            state.States[context.Key] = saved;
            return Task.FromResult(saved);
        }
    }
}

public sealed class StoredIdempotency
{
    public Dictionary<string, IdempotencyRecord> Records { get; } = new(StringComparer.Ordinal);
    public object Gate { get; } = new();
}

public sealed class TestIdempotencyStore(StoredIdempotency state, TimeProvider time) : IProcessIdempotencyStore
{
    public Task<IdempotencyRecord?> AdmitAsync(IdempotencyRecord admission, CancellationToken cancellationToken)
    {
        lock (state.Gate)
        {
            if (state.Records.TryGetValue(admission.Key, out var existing) && existing.ExpiresUtc > time.GetUtcNow())
            {
                return Task.FromResult<IdempotencyRecord?>(existing);
            }

            state.Records[admission.Key] = admission;
            return Task.FromResult<IdempotencyRecord?>(null);
        }
    }

    public Task<bool> CompleteAsync(IdempotencyRecord result, CancellationToken cancellationToken)
    {
        lock (state.Gate)
        {
            if (!state.Records.TryGetValue(result.Key, out var existing)
                || existing.AdmissionId != result.AdmissionId
                || existing.State != IdempotencyState.InProgress
                || existing.ExpiresUtc <= time.GetUtcNow())
            {
                return Task.FromResult(false);
            }

            state.Records[result.Key] = result;
            return Task.FromResult(true);
        }
    }

    public Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken)
    {
        lock (state.Gate)
        {
            return Task.FromResult(state.Records.TryGetValue(key, out var existing)
                && existing.AdmissionId == admissionId
                && existing.State == IdempotencyState.InProgress
                && existing.ExpiresUtc > time.GetUtcNow()
                && state.Records.Remove(key));
        }
    }
}

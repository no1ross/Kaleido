namespace Kaleido.Http.Client;

/// <summary>
/// Thread-safe fetch-once cache for a remote registry payload.
/// Owns the SemaphoreSlim and double-check locking pattern shared by
/// KaleidoProcessClient and KaleidoQueryableClient. Unlike HttpRegistryCache
/// (server-side, supports forceRefresh and partial-result passthrough), this
/// cache fetches once and never refreshes unless explicitly reset.
/// </summary>
internal sealed class HttpClientRegistryCache<T> : IDisposable
    where T : class
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private T? _value;

    public void Dispose() => _lock.Dispose();

    /// <summary>
    /// Clears the cached value so the next call to <see cref="GetOrFetchAsync"/>
    /// re-fetches from the remote endpoint. Non-blocking: an in-flight fetch
    /// may still store its result after the reset — that is a benign stale write
    /// (it was a valid fetch when it started), not a correctness hazard.
    /// </summary>
    public void Reset() =>
        Interlocked.Exchange(ref _value, null);

    public async Task<T> GetOrFetchAsync(
        Func<CancellationToken, Task<T>> fetch,
        CancellationToken cancellationToken)
    {
        if (_value is not null)
        {
            return _value;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_value is not null)
            {
                return _value;
            }

            _value = await fetch(cancellationToken);
            return _value;
        }
        finally
        {
            _lock.Release();
        }
    }
}

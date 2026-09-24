namespace Kaleido.Http.Client;

/// <summary>
/// Thread-safe fetch-once cache for a remote registry payload.
/// Owns the SemaphoreSlim and double-check locking pattern shared by
/// KaleidoProcessClient and KaleidoQueryableClient. Unlike HttpRegistryCache
/// (server-side, supports forceRefresh and partial-result passthrough), this
/// cache fetches once and never refreshes unless explicitly reset.
/// </summary>
internal sealed class HttpClientRegistryCache<T> : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private T? _value;

    public void Dispose() => _lock.Dispose();

    /// <summary>
    /// Clears the cached value so the next call to <see cref="GetOrFetchAsync"/>
    /// re-fetches from the remote endpoint.
    /// </summary>
    public void Reset()
    {
        _lock.Wait();
        try
        {
            _value = default;
        }
        finally
        {
            _lock.Release();
        }
    }

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

namespace Kaleido.AspNetCore.Registry;

/// <summary>
/// Singleton cache for the aggregated registry response.
/// Only stores fully-successful (zero <see cref="AggregatedRegistryResponse.ClientErrors"/>) results.
/// Partial results are served to callers but never committed to cache, so the last
/// clean snapshot remains available for subsequent calls.
/// </summary>
internal sealed class RegistryCache
{
    private volatile AggregatedRegistryResponse? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>The last fully-clean cached response, or <c>null</c> if none exists yet.</summary>
    public AggregatedRegistryResponse? Current => _cached;

    /// <summary>
    /// Returns the cached response if one exists and <paramref name="forceRefresh"/> is false.
    /// Otherwise, invokes <paramref name="build"/> to produce a fresh response.
    /// The cache is only updated when the fresh response has no <see cref="AggregatedRegistryResponse.ClientErrors"/>.
    /// </summary>
    public async Task<AggregatedRegistryResponse> GetOrBuildAsync(
        bool forceRefresh,
        Func<CancellationToken, Task<AggregatedRegistryResponse>> build,
        CancellationToken cancellationToken)
    {
        if (!forceRefresh && _cached is not null)
            return _cached;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _cached is not null)
                return _cached;

            var result = await build(cancellationToken);

            // Only advance the cache on a fully-clean result.
            // Partial results are returned to the caller so they can see ClientErrors,
            // but the last clean snapshot is preserved for non-refresh callers.
            if (result.ClientErrors.Count == 0)
                _cached = result;

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }
}

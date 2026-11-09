using Kaleido.Platform.Registry;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Kaleido.Registry;

internal interface IRegistryCache
{
    Task<RegistryView> GetViewAsync(Func<CancellationToken, Task<RegistryView>> build, CancellationToken cancellationToken);

    Task<KaleidoRegistry> GetCapabilitiesAsync(string revision, Func<CancellationToken, Task<KaleidoRegistry>> load, CancellationToken cancellationToken);

    Task InvalidateViewAsync(CancellationToken cancellationToken);
}

internal sealed partial class RegistryHybridCache(HybridCache cache, KaleidoRegistryCacheOptions options, ILogger<RegistryHybridCache> logger) : IRegistryCache
{
    private readonly string _viewKey = options.KeyPrefix + ":view";
    private readonly string _capabilityKeyPrefix = options.KeyPrefix + ":capabilities:";
    private readonly HybridCacheEntryOptions _viewOptions = new() { Expiration = options.ViewTtl, LocalCacheExpiration = options.ViewLocalTtl ?? options.ViewTtl };
    private readonly HybridCacheEntryOptions _capabilityOptions = new() { Expiration = options.CapabilitiesTtl, LocalCacheExpiration = options.CapabilitiesLocalTtl ?? options.CapabilitiesTtl };

    public async Task<RegistryView> GetViewAsync(Func<CancellationToken, Task<RegistryView>> build, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(_viewKey, build,
            static (factory, token) => new ValueTask<RegistryView>(factory(token)),
            _viewOptions, cancellationToken: cancellationToken);

    public async Task<KaleidoRegistry> GetCapabilitiesAsync(string revision, Func<CancellationToken, Task<KaleidoRegistry>> load, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(_capabilityKeyPrefix + revision, load,
            static (factory, token) => new ValueTask<KaleidoRegistry>(factory(token)),
            _capabilityOptions, cancellationToken: cancellationToken);

    public async Task InvalidateViewAsync(CancellationToken cancellationToken)
    {
        try
        {
            await cache.RemoveAsync(_viewKey, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogViewInvalidationFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The central registry's cached view could not be invalidated; it will expire at its configured lifetime.")]
    private static partial void LogViewInvalidationFailed(ILogger logger, Exception exception);
}

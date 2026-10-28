using Kaleido.Http.Registry;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http;

/// <summary>
/// Chooses where registry snapshots are stored; the entry point is <see cref="AddDistributedRegistrySnapshots"/>.
/// </summary>
public static class KaleidoRegistrySnapshotServiceCollectionExtensions
{
    /// <summary>
    /// Stores registry snapshots in the host's <see cref="IDistributedCache"/> instead of in memory, so
    /// the replicas of a service share them (#275):
    /// <list type="bullet">
    /// <item>a router's merged snapshot (<c>kaleido:{ServiceName}</c>): one replica rebuilds it per
    /// <c>RegistryCacheTtl</c> and the others serve it;</item>
    /// <item>each client's copy of a downstream registry (<c>kaleido-client:{ServiceName}:{clientName}</c>).</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host registers the cache itself, e.g. <c>services.AddStackExchangeRedisCache(...)</c>; Kaleido
    /// references no cache technology. Call it before or after <c>AddHttp()</c> and <c>AddHttpClients()</c>:
    /// it replaces their in-memory default, and a store registered earlier.
    /// </para>
    /// <para>
    /// The cache never fails a registry read: every snapshot is also kept in memory, and while the cache
    /// is unreachable the replica serves its own snapshots (logged once as a warning) and tries the cache
    /// again 30 seconds later. Snapshots carry no cache expiry; freshness stays with <c>RegistryCacheTtl</c>
    /// and <c>RegistryTtl</c>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddDistributedRegistrySnapshots(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Every piece built by the container (#498): the snapshots also kept in memory, the clock that
        // times the cache's retry, and the store itself.
        builder.Services.TryAddKeyedSingleton<IRegistrySnapshotStore, InMemoryRegistrySnapshotStore>(DistributedRegistrySnapshotStore.MemoryKey);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.Replace(ServiceDescriptor.Singleton<IRegistrySnapshotStore, DistributedRegistrySnapshotStore>());

        return builder;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client;

/// <summary>
/// Makes a Kaleido host a participant of the control registry (#553); the entry point is
/// <see cref="AddControlRegistryClient"/>.
/// </summary>
public static class KaleidoControlRegistryClientServiceCollectionExtensions
{
    /// <summary>
    /// Makes this host's registry the control registry's (<c>Kaleido:Registry:Url</c>): its full registry,
    /// kept in memory and fresh - the capabilities checked every
    /// <see cref="KaleidoRegistryClientOptions.CapabilitiesInterval"/>, each version's availability every
    /// <see cref="KaleidoRegistryClientOptions.AvailabilityInterval"/>, each with its ETag - is what this
    /// host serves, filtered per caller, and what its lookups, hand-offs and MCP read. It replaces the
    /// per-client fan-out of <c>AddHttpClients()</c>.
    /// </summary>
    /// <remarks>
    /// The calls (named client <c>kaleido-registry</c>) are the service's own: with an
    /// <see cref="IKaleidoCallCredentialProvider"/> it decides their credential (purpose
    /// <see cref="KaleidoCallPurpose.ServiceOwn"/>, service <see cref="KaleidoRegistryClientOptions.ServiceName"/>),
    /// which must satisfy the registry's reader policy.
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="Kaleido.Exceptions.KaleidoConfigurationException">
    /// <c>Kaleido:Registry</c> is invalid (<see cref="HttpClientErrorCodes.InvalidClientOptions"/>), e.g. no
    /// <c>Url</c>.
    /// </exception>
    public static IKaleidoBuilder AddControlRegistryClient(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Configuration
            .GetSection($"{KaleidoServiceOptions.SectionName}:{KaleidoRegistryClientOptions.SectionName}")
            .BindOver(new KaleidoRegistryClientOptions(), HttpClientErrorCodes.InvalidClientOptions);
        KaleidoRegistryClientOptions.Validate(options);

        var services = builder.Services;
        services.Replace(ServiceDescriptor.Singleton(options));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IKaleidoClientCatalog, KaleidoClientCatalog>();

        // Its own short timeout and no retries: the sync tries again at its next check.
        services.AddHttpClient(CentralRegistrySource.ClientName, client =>
        {
            client.BaseAddress = new Uri((options.Url ?? string.Empty).TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.TryAddKeyedTransient<KaleidoCallCredentialHandler>(CentralRegistrySource.ClientName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoCallCredentialHandlerFilter>());

        services.TryAddSingleton<CentralRegistrySource>();
        services.TryAddSingleton<ICentralRegistrySource>(x => x.GetRequiredService<CentralRegistrySource>());
        services.Replace(ServiceDescriptor.Singleton<IKaleidoRegistrySource>(x => x.GetRequiredService<CentralRegistrySource>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CentralRegistrySync>());

        return builder;
    }
}

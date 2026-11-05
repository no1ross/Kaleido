using Kaleido.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
    /// Registers this instance with the control registry (<c>Kaleido:Registry:Url</c>) and heartbeats while it
    /// runs, and makes this host's registry the control registry's: its full registry,
    /// kept in memory and fresh - the capabilities and each version's availability, each with its ETag, checked
    /// at the intervals the registry hands out (the host sets none) - is what this host serves, filtered per
    /// caller, and what its lookups, hand-offs and MCP read. It replaces the per-client fan-out of
    /// <c>AddHttpClients()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The instance registers at startup (its service, <c>ServiceVersion</c>, <c>InstanceId</c> and its own
    /// capabilities - never where it runs: the router maps its route to its endpoint), retrying until it succeeds,
    /// then heartbeats at the interval the registry hands out, with its health from local checks only; it
    /// registers again when the registry no longer knows it, and deregisters at shutdown. The readiness check
    /// <c>kaleido-registration</c> (tagged <c>ready</c>) is unhealthy until it has registered and read the
    /// registry once; a later failure never makes the instance not ready.
    /// </para>
    /// <para>
    /// The calls (named client <c>kaleido-registry</c>) are the service's own
    /// (<see cref="KaleidoCallPurpose.ServiceOwn"/>), never a caller's: they carry what the host's handlers set,
    /// typically an app-only token (client credentials) added with
    /// <see cref="KaleidoHttpClientsServiceCollectionExtensions.AddKaleidoClientHandler{THandler}"/>, which must
    /// satisfy the registry's registrant and reader policies (#574).
    /// </para>
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="Kaleido.Exceptions.KaleidoConfigurationException">
    /// <c>Kaleido:Registry</c> is invalid (<see cref="HttpClientErrorCodes.InvalidClientOptions"/>), e.g. no
    /// <c>Url</c>, or the service has no <c>ServiceVersion</c> (the registry requires one).
    /// </exception>
    public static IKaleidoBuilder AddControlRegistryClient(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Configuration
            .GetSection($"{KaleidoServiceOptions.SectionName}:{KaleidoRegistryClientOptions.SectionName}")
            .BindOver(new KaleidoRegistryClientOptions(), HttpClientErrorCodes.InvalidClientOptions);
        KaleidoRegistryClientOptions.Validate(options);

        if (string.IsNullOrWhiteSpace(builder.ServiceOptions.ServiceVersion))
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.InvalidClientOptions,
                "Kaleido:ServiceVersion is required to register with the control registry: every registered service names its version (any label, e.g. '2026-11').");
        }

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
        services.TryAddKeyedTransient<KaleidoOnBehalfOfGuard>(CentralRegistrySource.ClientName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoClientHandlerFilter>());

        services.TryAddSingleton<CentralRegistrySource>();
        services.TryAddSingleton<ICentralRegistrySource>(x => x.GetRequiredService<CentralRegistrySource>());
        services.Replace(ServiceDescriptor.Singleton<IKaleidoRegistrySource>(x => x.GetRequiredService<CentralRegistrySource>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CentralRegistrySync>());

        services.TryAddSingleton<ControlRegistryRegistrar>();
        services.TryAddSingleton<IControlRegistryRegistrar>(x => x.GetRequiredService<ControlRegistryRegistrar>());

        // Once per host: the registrar runs, and its readiness check reports.
        if (!services.Any(x => x.ServiceType == typeof(RegistrationHealthCheck)))
        {
            services.AddHostedService(x => x.GetRequiredService<ControlRegistryRegistrar>());
            services.AddSingleton<RegistrationHealthCheck>();
            services.AddHealthChecks().Add(new HealthCheckRegistration(
                RegistrationHealthCheck.Name,
                x => x.GetRequiredService<RegistrationHealthCheck>(),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["kaleido", "registry", "ready"]));
        }

        return builder;
    }
}

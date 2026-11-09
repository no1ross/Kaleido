using Kaleido.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.Registry;

/// <summary>
/// Makes a Kaleido host a participant of the control registry (#553); the entry point is
/// <see cref="AddControlRegistryClient"/>.
/// </summary>
public static class KaleidoControlRegistryClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers this instance with the central registry through the gateway (<c>Kaleido:BaseUrl</c>)
    /// and heartbeats while it runs. The host keeps the full registry hydrated in memory: capabilities
    /// and availability have separate ETags and the registry controls their check interval. In-host
    /// lookups, hand-offs and MCP read that copy; there is no per-service registry endpoint.
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
    /// Registration, heartbeat and hydration calls (named client <c>kaleido-registry</c>) are the
    /// service's own (<see cref="KaleidoCallPurpose.ServiceOwn"/>): they carry what the host's handlers set,
    /// typically an app-only token from
    /// <see cref="Kaleido.Http.Client.Registration.KaleidoHttpClientsServiceCollectionExtensions.AddKaleidoClientHandler{THandler}"/>, which must
    /// satisfy the registry's registrant and reader policies (#574). A separate caller-filtered read
    /// through <see cref="IKaleidoRegistryClient.GetRegistryAsync"/> uses the inbound caller.
    /// </para>
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="Kaleido.Exceptions.KaleidoConfigurationException">
    /// <c>Kaleido:BaseUrl</c> is not an absolute gateway URL (<see cref="HttpClientErrorCodes.InvalidClientOptions"/>),
    /// or the service has no <c>ServiceVersion</c> (the registry requires one).
    /// </exception>
    public static IKaleidoBuilder AddControlRegistryClient(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var gateway = builder.Configuration[$"{KaleidoServiceOptions.SectionName}:{nameof(KaleidoClientOptions.BaseUrl)}"];
        if (!Uri.TryCreate(gateway, UriKind.Absolute, out var address) || address.Scheme is not ("http" or "https") || address.UserInfo.Length > 0)
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.InvalidClientOptions,
                "Kaleido:BaseUrl must be the gateway's absolute http or https URL without embedded credentials to use the central registry client.");
        }

        if (string.IsNullOrWhiteSpace(builder.ServiceOptions.ServiceVersion))
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.InvalidClientOptions,
                "Kaleido:ServiceVersion is required to register with the control registry: every registered service names its version (any label, e.g. '2026-11').");
        }

        var services = builder.Services;
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IKaleidoInboundCallerAccessor, KaleidoInboundCallerAccessor>();

        AddRegistryHttpClient(builder, gateway);

        services.TryAddScoped<IKaleidoRegistryClient, KaleidoHttpRegistryClient>();
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

    private static void AddRegistryHttpClient(IKaleidoBuilder builder, string gateway)
    {
        var services = builder.Services;
        // Its own short timeout and the shared safe-call resilience; the sync tries again at its next check.
        var resilience = builder.Configuration
            .GetSection($"{KaleidoServiceOptions.SectionName}:{nameof(KaleidoClientOptions.Resilience)}")
            .BindOver(new KaleidoResilienceOptions(), HttpClientErrorCodes.InvalidClientOptions);
        KaleidoResilienceOptions.Validate(resilience, "Kaleido:Resilience");
        var httpClient = services.AddHttpClient(KaleidoHttpRegistryClient.ClientName, client =>
        {
            client.BaseAddress = new Uri(gateway.TrimEnd('/') + "/");
            client.Timeout = resilience.Enabled ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(10);
        });
        if (resilience.Enabled)
        {
            httpClient.AddResilienceHandler("kaleido", pipeline => pipeline.AddKaleidoResilience(resilience));
        }
        services.TryAddKeyedTransient<KaleidoOnBehalfOfGuard>(KaleidoHttpRegistryClient.ClientName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter, KaleidoClientHandlerFilter>());
    }
}

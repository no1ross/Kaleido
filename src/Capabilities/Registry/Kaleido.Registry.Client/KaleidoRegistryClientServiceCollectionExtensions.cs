using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Registry.Client;

/// <summary>Opt-in transport-neutral Registry participation for a service host.</summary>
public static class KaleidoRegistryClientServiceCollectionExtensions
{
    /// <summary>Registers local publication, lease renewal and a hydrated Registry source; a transport registers <see cref="IKaleidoRegistryClient"/> separately.</summary>
    /// <param name="builder">The host builder with its service identity and capability registrations.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddRegistryClient(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<CentralRegistrySource>();
        services.TryAddSingleton<ICentralRegistrySource>(provider => provider.GetRequiredService<CentralRegistrySource>());
        services.Replace(ServiceDescriptor.Singleton<IKaleidoRegistrySource>(provider => provider.GetRequiredService<CentralRegistrySource>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CentralRegistrySync>());
        services.TryAddSingleton<IKaleidoTransportSelector, KaleidoTransportSelector>();
        services.TryAddScoped<IKaleidoRegistryTargetResolver, KaleidoRegistryTargetResolver>();
        services.TryAddSingleton<ControlRegistryRegistrar>();
        services.TryAddSingleton<IControlRegistryRegistrar>(provider => provider.GetRequiredService<ControlRegistryRegistrar>());

        if (!services.Any(x => x.ServiceType == typeof(RegistrationHealthCheck)))
        {
            services.AddHostedService(provider => provider.GetRequiredService<ControlRegistryRegistrar>());
            services.AddSingleton<RegistrationHealthCheck>();
            services.AddHealthChecks().Add(new HealthCheckRegistration(
                RegistrationHealthCheck.Name,
                provider => provider.GetRequiredService<RegistrationHealthCheck>(),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["kaleido", "registry", "ready"]));
        }

        return builder;
    }
}

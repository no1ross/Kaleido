using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Hosting;

/// <summary>Composes transport-neutral Kaleido capabilities for a host.</summary>
public static class KaleidoHostingServiceCollectionExtensions
{
    /// <summary>Registers Server authorization, discovered Process and Queryable runtimes, and their event writer.</summary>
    /// <remarks>
    /// Call <c>AddKaleido()</c> first with a service name, version and assemblies. Hosting passes the
    /// service-wide authorization default to capabilities without their own default; each capability's
    /// configuration can override it. Eventing is registered only when a capability is present or
    /// <paramref name="eventOptions"/> explicitly requests an Eventing-only host. The host selects
    /// its stores separately; a startup check reports missing registrations. No transport, central
    /// registry client, outbox relay or business job is installed by this method.
    /// </remarks>
    /// <param name="builder">The configured Kaleido builder.</param>
    /// <param name="serverOptions">Transport-neutral authorization options from code, overridden by configuration.</param>
    /// <param name="eventOptions">Eventing options, or <see langword="null"/> for defaults when a capability uses events.</param>
    /// <param name="processOptions">Process settings; a missing authorization default inherits the service rule.</param>
    /// <param name="queryableOptions">Queryable settings; a missing authorization default inherits the service rule.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    /// <exception cref="KaleidoConfigurationException">Hosting was registered twice, or owner settings or registrations are invalid.</exception>
    public static IKaleidoBuilder AddHosting(
        this IKaleidoBuilder builder,
        KaleidoServerOptions? serverOptions = null,
        KaleidoEventOptions? eventOptions = null,
        KaleidoProcessOptions? processOptions = null,
        KaleidoQueryableOptions? queryableOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        if (services.Any(x => x.ServiceType == typeof(IHostedService) && x.ImplementationInstance is KaleidoProviderRegistrationCheck))
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.DuplicateRegistration, "AddHosting() was called more than once on this service collection.");
        }

        builder.AddServer(serverOptions);
        var serviceRule = services
            .Where(x => x.ServiceType == typeof(KaleidoServerOptions))
            .Select(x => x.ImplementationInstance)
            .OfType<KaleidoServerOptions>()
            .Single()
            .DefaultAuthorization;

        var process = processOptions ?? new KaleidoProcessOptions();
        builder.AddProcess(process.DefaultAuthorization is null ? process with { DefaultAuthorization = serviceRule } : process);

        var queryable = queryableOptions ?? new KaleidoQueryableOptions();
        builder.AddQueryable(queryable.DefaultAuthorization is null ? queryable with { DefaultAuthorization = serviceRule } : queryable);

        if (eventOptions is not null || services.Any(x => x.ServiceType == typeof(IProcessorRuntime) || x.ServiceType == typeof(IQueryableService)))
        {
            builder.AddEventing(eventOptions);
        }

        services.AddSingleton<IHostedService>(new KaleidoProviderRegistrationCheck(services));
        return builder;
    }
}

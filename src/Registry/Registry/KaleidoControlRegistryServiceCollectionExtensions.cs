using Kaleido.Platform.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Registry;

/// <summary>
/// Registers the control registry (#553) on an <see cref="IKaleidoBuilder"/>; the entry point is
/// <see cref="AddControlRegistry"/>.
/// </summary>
public static class KaleidoControlRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Makes this host the control registry: the central registry every Kaleido service registers with and
    /// heartbeats to. It keeps one record per service and version, each instance's status, and each
    /// service's current version, and serves the merged registry (<c>MapKaleidoControlRegistry()</c>).
    /// </summary>
    /// <remarks>
    /// Settings come from <c>Kaleido:Registry</c> (<see cref="KaleidoRegistryOptions"/>);
    /// <see cref="KaleidoRegistryOptions.RegistrantPolicy"/> and <see cref="KaleidoRegistryOptions.ReaderPolicy"/>
    /// are required unless <c>AuthorizationMode</c> is <c>None</c>. Call <c>AddHttp()</c>
    /// too, and choose a store: <c>AddSqliteRegistryStore</c>, <see cref="AddRegistryStore{TStore}"/>, or
    /// <see cref="AddInMemoryRegistryStore"/>. Health checks: <c>kaleido-registry-store</c> (tagged
    /// <c>ready</c>: the registry's readiness depends only on its store) and
    /// <c>kaleido-registry-completeness</c> (Degraded while an expected version is missing or has no ready
    /// instance).
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="KaleidoConfigurationException">An invalid <c>Kaleido:Registry</c> setting, or no reader policy under enforcement.</exception>
    public static IKaleidoBuilder AddControlRegistry(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = builder.Configuration
            .GetSection($"{KaleidoServiceOptions.SectionName}:{KaleidoRegistryOptions.SectionName}")
            .BindOver(new KaleidoRegistryOptions(), ConfigurationErrorCodes.InvalidOptions);
        KaleidoRegistryOptions.Validate(options);

        // Only service applications register, and only Kaleido hosts read: under enforcement, the policies
        // that admit them are required.
        if (builder.ServiceOptions.AuthorizationMode != KaleidoAuthorizationMode.None)
        {
            if (string.IsNullOrWhiteSpace(options.RegistrantPolicy))
            {
                throw new KaleidoConfigurationException(
                    ConfigurationErrorCodes.InvalidOptions,
                    "Kaleido:Registry:RegistrantPolicy is required: only the service applications it admits register, renew and remove instances. Name the authorization policy their app-only tokens satisfy (never a user's).");
            }

            if (string.IsNullOrWhiteSpace(options.ReaderPolicy))
            {
                throw new KaleidoConfigurationException(
                    ConfigurationErrorCodes.InvalidOptions,
                    "Kaleido:Registry:ReaderPolicy is required: only the Kaleido hosts it admits read the control registry. Name the authorization policy their service tokens satisfy.");
            }
        }

        var services = builder.Services;
        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRegistryRegistrants, RegistryRegistrants>();
        services.TryAddScoped<IControlRegistry, ControlRegistry>();

        // One full registry per host, built from the store: the registry service, the status endpoint and
        // the completeness check share it.
        services.TryAddSingleton<StoreRegistrySource>();
        services.Replace(ServiceDescriptor.Singleton<IKaleidoRegistrySource>(x => x.GetRequiredService<StoreRegistrySource>()));
        services.TryAddSingleton<IRegistryStatusReader>(x => x.GetRequiredService<StoreRegistrySource>());

        services.AddHostedService<RegistryHousekeeping>();
        services.AddHealthChecks()
            .AddCheck<RegistryStoreHealthCheck>(RegistryStoreHealthCheck.Name, failureStatus: null, tags: ["kaleido", "registry", "ready"])
            .AddCheck<RegistryCompletenessHealthCheck>(RegistryCompletenessHealthCheck.Name, failureStatus: null, tags: ["kaleido", "registry"]);

        return builder;
    }

    /// <summary>
    /// Keeps the control registry's state in this process only, chosen explicitly: lost on restart and not
    /// shared between registry nodes. For development, tests and demos.
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingStore"/>: another registry store was registered.
    /// </exception>
    public static IKaleidoBuilder AddInMemoryRegistryStore(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        EnsureNoOtherStore(builder.Services, typeof(InMemoryKaleidoRegistryStore));
        builder.Services.TryAddSingleton<IKaleidoRegistryStore, InMemoryKaleidoRegistryStore>();

        return builder;
    }

    /// <summary>
    /// Keeps the control registry's state in <typeparamref name="TStore"/>, registered as scoped: a provider's
    /// store, or a platform's own over its own database.
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="KaleidoConfigurationException">
    /// <see cref="ConfigurationErrorCodes.ConflictingStore"/>: another registry store was registered.
    /// </exception>
    public static IKaleidoBuilder AddRegistryStore<TStore>(this IKaleidoBuilder builder)
        where TStore : class, IKaleidoRegistryStore
    {
        ArgumentNullException.ThrowIfNull(builder);

        EnsureNoOtherStore(builder.Services, typeof(TStore));
        builder.Services.TryAddScoped<IKaleidoRegistryStore, TStore>();

        return builder;
    }

    private static void EnsureNoOtherStore(IServiceCollection services, Type store)
    {
        if (services.LastOrDefault(x => x.ServiceType == typeof(IKaleidoRegistryStore)) is { } registered
            && (registered.ImplementationType ?? registered.ImplementationInstance?.GetType() ?? typeof(IKaleidoRegistryStore)) != store)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ConflictingStore,
                $"Two registry stores were registered: {(registered.ImplementationType ?? typeof(IKaleidoRegistryStore)).Name} and {store.Name}. The control registry has one store.");
        }
    }
}

using Kaleido.Http.Client.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Http.Client;

internal static class KaleidoClientServiceCollectionExtensions
{
    // One configured client (#498): its definition in the catalog, its named HttpClient, and its keyed
    // services - the process and queryable clients, the health check and the credential handler -
    // registered by type, so the container builds every one of them. Registering a name twice is a
    // no-op (AddHttpClients() may be called more than once).
    internal static IKaleidoBuilder AddKaleidoClient(
        this IKaleidoBuilder builder,
        KaleidoHttpClientOptions options,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BaseUrl, nameof(options));

        var services = builder.Services;

        AddSharedServices(services);

        if (services.Any(x => x.ImplementationInstance is KaleidoClientDefinition known &&
                              string.Equals(known.Name, options.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return builder;
        }

        services.AddSingleton(new KaleidoClientDefinition(
            options.Name,
            options.RoutePrefix,
            options.RegistryTtl,
            options.StrictRegistryProbe,
            options.Resilience?.HealthCheckTimeout ?? new KaleidoResilienceOptions().HealthCheckTimeout));

        var httpClientBuilder = services.AddHttpClient(options.Name, client => client.BaseAddress = new Uri(options.BaseUrl));

        configureClient?.Invoke(httpClientBuilder);

        if (options.Resilience is { Enabled: true } resilience)
        {
            httpClientBuilder.AddResilienceHandler("kaleido", pipeline => pipeline.AddKaleidoResilience(resilience));
        }

        services.AddKeyedScoped<IKaleidoProcessorClient, KaleidoProcessorClient>(options.Name);
        services.AddKeyedScoped<IKaleidoQueryableClient, KaleidoQueryableClient>(options.Name);
        services.AddKeyedTransient<KaleidoCallCredentialHandler>(options.Name);
        services.AddKeyedTransient<KaleidoClientHealthCheck>(options.Name);

        AddClientHealthCheck(services, options.Name);

        return builder;
    }

    // What every client shares, registered once (TryAdd*).
    private static void AddSharedServices(IServiceCollection services)
    {
        services.TryAddSingleton<IKaleidoClientCatalog, KaleidoClientCatalog>();
        services.TryAddSingleton<Registry.IRegistrySnapshotStore, Registry.InMemoryRegistrySnapshotStore>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IHttpClientRegistryCache, HttpClientRegistryCache>();
        services.TryAddSingleton<IKaleidoRemoteRegistry, KaleidoRemoteRegistry>();
        services.TryAddSingleton<IKaleidoProcessForwarding, KaleidoProcessForwarding>();
        services.TryAddScoped<IKaleidoInboundCallerAccessor, KaleidoInboundCallerAccessor>();
        services.TryAddScoped<ICorrelationHeaderStamper, CorrelationHeaderStamper>();
        services.TryAddScoped<IKaleidoClientTransport, KaleidoClientTransport>();
        services.TryAddScoped<IKaleidoProcessorClientFactory, KaleidoProcessorClientFactory>();
        services.TryAddScoped<IKaleidoQueryableClientFactory, KaleidoQueryableClientFactory>();
    }

    // A health check that probes the remote registry endpoint, through the same named HttpClient so
    // BaseAddress and any configured delegating handlers apply (never retried, own short timeout). The
    // check is the client's keyed service; the registration only resolves it. Configure<...> runs at
    // resolution time, so the guard inside skips duplicate names.
    private static void AddClientHealthCheck(IServiceCollection services, string clientName)
    {
        var healthCheckName = $"kaleido-{clientName}";

        services.AddHealthChecks();
        services.Configure<HealthCheckServiceOptions>(o =>
        {
            if (o.Registrations.Any(r => string.Equals(r.Name, healthCheckName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            o.Registrations.Add(new HealthCheckRegistration(
                name: healthCheckName,
                factory: sp => sp.GetRequiredKeyedService<KaleidoClientHealthCheck>(clientName),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["kaleido", "remote"]));
        });
    }
}

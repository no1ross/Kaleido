using Kaleido.Http.Client.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Http.Client;

internal static class KaleidoClientServiceCollectionExtensions
{
    internal static IServiceCollection AddKaleidoClient<TClient, TMap, TFactory, TFactoryInterface>(
        this IServiceCollection services,
        Action<KaleidoHttpClientOptions> configure,
        Func<string, string> registryUrlFactory,
        Action<IHttpClientBuilder>? configureClient = null)
        where TClient : class
        where TMap : class, IKaleidoClientRouteOptionsMap, new()
        where TFactory : class, TFactoryInterface
        where TFactoryInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KaleidoHttpClientOptions();
        configure(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name, nameof(configure));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BaseUrl, nameof(configure));

        var httpClientBuilder = services.AddHttpClient(options.Name, client => client.BaseAddress = new Uri(options.BaseUrl));

        configureClient?.Invoke(httpClientBuilder);

        // The processor and the queryable client share the named HttpClient: one pipeline per name.
        if (options.Resilience is { Enabled: true } resilience && GetOrAddResilientClients(services).Add(options.Name))
        {
            httpClientBuilder.AddResilienceHandler("kaleido", pipeline => pipeline.AddKaleidoResilience(resilience));
        }

        var routeOptions = GetOrAddRouteOptions<TMap>(services);
        routeOptions.SetRoutePrefix(options.Name, options.RoutePrefix);

        if (options.RegistryTtl.HasValue)
        {
            routeOptions.SetRegistryTtl(options.Name, options.RegistryTtl.Value);
        }

        services.TryAddSingleton<Registry.IRegistrySnapshotStore, Registry.InMemoryRegistrySnapshotStore>();
        services.TryAddSingleton<KaleidoRemoteRegistry>();
        services.TryAddSingleton<IKaleidoProcessForwarding, KaleidoProcessForwarding>();
        services.TryAddScoped<IKaleidoInboundCallerAccessor, KaleidoInboundCallerAccessor>();
        services.TryAddScoped<ICorrelationHeaderStamper, CorrelationHeaderStamper>();
        services.TryAddScoped<TFactoryInterface, TFactory>();

        AddClientHealthCheck(services, options, registryUrlFactory);

        return services;
    }

    // A health check that probes the remote registry endpoint, through the same named HttpClient so
    // BaseAddress and any configured delegating handlers apply (never retried, own short timeout).
    // Configure<HealthCheckServiceOptions> runs at resolution time, so the guard inside safely skips
    // duplicate names even when AddHttpClients() is called multiple times.
    private static void AddClientHealthCheck(
        IServiceCollection services,
        KaleidoHttpClientOptions options,
        Func<string, string> registryUrlFactory)
    {
        var clientName = options.Name;
        var healthCheckName = $"kaleido-{clientName}";
        var healthCheckTimeout = options.Resilience?.HealthCheckTimeout ?? new KaleidoResilienceOptions().HealthCheckTimeout;
        var registryPath =
            options.StrictRegistryProbe
                ? $"{registryUrlFactory(options.RoutePrefix)}?strict"
                : registryUrlFactory(options.RoutePrefix);

        services.AddHealthChecks();
        services.Configure<HealthCheckServiceOptions>(o =>
        {
            if (o.Registrations.Any(r => string.Equals(r.Name, healthCheckName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            o.Registrations.Add(new HealthCheckRegistration(
                name: healthCheckName,
                factory: sp => new KaleidoClientHealthCheck(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    clientName,
                    registryPath,
                    healthCheckTimeout,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KaleidoClientHealthCheck>>()),
                failureStatus: HealthStatus.Unhealthy,
                tags: ["kaleido", "remote"]));
        });
    }

    internal static IKaleidoBuilder AddProcessorClient(
        this IKaleidoBuilder builder,
        Action<KaleidoHttpClientOptions> configure,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKaleidoClient<IKaleidoProcessorClient, KaleidoProcessorClientRouteOptionsMap, KaleidoProcessorClientFactory, IKaleidoProcessorClientFactory>(
            configure,
            Registry.RegistryContractUrls.Registry,
            configureClient);

        return builder;
    }

    internal static IKaleidoBuilder AddQueryableClient(
        this IKaleidoBuilder builder,
        Action<KaleidoHttpClientOptions> configure,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKaleidoClient<IKaleidoQueryableClient, KaleidoQueryableClientRouteOptionsMap, KaleidoQueryableClientFactory, IKaleidoQueryableClientFactory>(
            configure,
            Registry.RegistryContractUrls.Registry,
            configureClient);

        return builder;
    }

    private static ResilientClients GetOrAddResilientClients(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(ResilientClients))?.ImplementationInstance is ResilientClients existing)
        {
            return existing;
        }

        var clients = new ResilientClients();
        services.AddSingleton(clients);
        return clients;
    }

    // The client names that already have a resilience pipeline (registration time only).
    private sealed class ResilientClients
    {
        private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

        public bool Add(string name) => _names.Add(name);
    }

    private static TMap GetOrAddRouteOptions<TMap>(IServiceCollection services)
        where TMap : class, IKaleidoClientRouteOptionsMap, new()
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TMap));

        if (descriptor?.ImplementationInstance is TMap existing)
        {
            return existing;
        }

        var map = new TMap();
        services.AddSingleton(map);
        return map;
    }
}

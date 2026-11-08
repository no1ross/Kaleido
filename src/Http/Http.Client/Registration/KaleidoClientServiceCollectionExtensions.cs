using Kaleido.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http.Client.Registration;

internal static class KaleidoClientServiceCollectionExtensions
{
    // One configured client (#498): its definition in the catalog, its named HttpClient, and its keyed
    // services - the process and queryable clients and the on-behalf-of guard -
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

        services.AddSingleton(new KaleidoClientDefinition(options.Name, options.RoutePrefix));

        var httpClientBuilder = services.AddHttpClient(options.Name, client => client.BaseAddress = new Uri(options.BaseUrl));

        configureClient?.Invoke(httpClientBuilder);

        if (options.Resilience is { Enabled: true } resilience)
        {
            httpClientBuilder.AddResilienceHandler("kaleido", pipeline => pipeline.AddKaleidoResilience(resilience));
        }

        services.AddKeyedScoped<IKaleidoProcessorClient, KaleidoProcessorClient>(options.Name);
        services.AddKeyedScoped<IKaleidoQueryableClient, KaleidoQueryableClient>(options.Name);
        services.AddKeyedTransient<KaleidoOnBehalfOfGuard>(options.Name);
        return builder;
    }

    // What every client shares, registered once (TryAdd*).
    private static void AddSharedServices(IServiceCollection services)
    {
        services.TryAddSingleton<IKaleidoClientCatalog, KaleidoClientCatalog>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IKaleidoProcessForwarding, KaleidoProcessForwarding>();
        services.TryAddScoped<IKaleidoInboundCallerAccessor, KaleidoInboundCallerAccessor>();
        services.TryAddScoped<ICorrelationHeaderStamper, CorrelationHeaderStamper>();
        services.TryAddScoped<IKaleidoClientTransport, KaleidoClientTransport>();
        services.TryAddScoped<IKaleidoProcessorClientFactory, KaleidoProcessorClientFactory>();
        services.TryAddScoped<IKaleidoQueryableClientFactory, KaleidoQueryableClientFactory>();
    }

}

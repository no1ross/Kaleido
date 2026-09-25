using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http.Client;

internal static class KaleidoClientExtensions
{
    internal static IServiceCollection AddKaleidoClient<TClient, TMap, TFactory, TFactoryInterface>(
        this IServiceCollection services,
        Action<KaleidoHttpClientOptions> configure,
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

        var httpClientBuilder = services.AddHttpClient(options.Name, client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        configureClient?.Invoke(httpClientBuilder);

        var routeOptions = GetOrAddRouteOptions<TMap>(services);
        routeOptions.Options[options.Name] = options.RoutePrefix;

        services.TryAddScoped<ICorrelationHeaderStamper, CorrelationHeaderStamper>();
        services.TryAddScoped<TFactoryInterface, TFactory>();

        return services;
    }

    internal static IKaleidoBuilder AddProcessClient(
        this IKaleidoBuilder builder,
        Action<KaleidoHttpClientOptions> configure,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKaleidoClient<IKaleidoProcessClient, KaleidoProcessClientRouteOptionsMap, KaleidoProcessClientFactory, IKaleidoProcessClientFactory>(
            configure,
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
            configureClient);

        return builder;
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

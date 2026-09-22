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
        where TMap : class, new()
        where TFactory : class, TFactoryInterface
        where TFactoryInterface : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KaleidoHttpClientOptions();
        configure(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BaseUrl);

        var httpClientBuilder = services.AddHttpClient(options.Name, client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        configureClient?.Invoke(httpClientBuilder);

        var routeOptions = GetOrAddRouteOptions<TMap>(services);
        var optionsProperty = typeof(TMap).GetProperty("Options");

        if (optionsProperty != null && optionsProperty.GetValue(routeOptions) is Dictionary<string, string> optionsDict)
        {
            optionsDict[options.Name] = options.RoutePrefix;
        }

        services.TryAddScoped<ICorrelationHeaderStamper, CorrelationHeaderStamper>();
        services.TryAddScoped<TFactoryInterface, TFactory>();

        return services;
    }

    private static TMap GetOrAddRouteOptions<TMap>(IServiceCollection services)
        where TMap : class, new()
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TMap));

        if (descriptor?.ImplementationInstance is TMap existing)
            return existing;

        var map = new TMap();
        services.AddSingleton(map);
        return map;
    }
}

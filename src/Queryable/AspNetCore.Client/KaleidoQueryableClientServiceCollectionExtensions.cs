using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoQueryableClientServiceCollectionExtensions
{
    public static IKaleidoBuilder AddQueryableClient(
        this IKaleidoBuilder builder,
        Action<KaleidoQueryableClientOptions> configure,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KaleidoQueryableClientOptions();
        configure(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BaseUrl);

        var httpClientBuilder = builder.Services.AddHttpClient(options.Name, client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        configureClient?.Invoke(httpClientBuilder);

        // Accumulate per-name route options into a shared singleton dictionary.
        // Multiple AddQueryableClient calls each add their entry before the factory resolves.
        var routeOptions = GetOrAddRouteOptions(builder.Services);
        routeOptions[options.Name] = new QueryableRouteOptions { RoutePrefix = options.RoutePrefix };

        builder.Services.TryAddScoped<IKaleidoQueryableClientFactory, KaleidoQueryableClientFactory>();

        return builder;
    }

    private static Dictionary<string, QueryableRouteOptions> GetOrAddRouteOptions(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(KaleidoQueryableClientRouteOptionsMap));

        if (descriptor?.ImplementationInstance is KaleidoQueryableClientRouteOptionsMap existing)
            return existing.Options;

        var map = new KaleidoQueryableClientRouteOptionsMap();
        services.AddSingleton(map);
        return map.Options;
    }
}

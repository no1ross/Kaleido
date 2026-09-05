using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoQueryableClientServiceCollectionExtensions
{
    public static IKaleidoBuilder AddQueryableClient(
        this IKaleidoBuilder builder,
        string name,
        string baseUrl,
        string routePrefix = "")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        builder.Services.AddHttpClient(name, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });

        // Accumulate per-name route options into a shared singleton dictionary.
        // Multiple AddQueryableClient calls each add their entry before the factory resolves.
        var routeOptions = GetOrAddRouteOptions(builder.Services);
        routeOptions[name] = new QueryableRouteOptions { RoutePrefix = routePrefix };

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

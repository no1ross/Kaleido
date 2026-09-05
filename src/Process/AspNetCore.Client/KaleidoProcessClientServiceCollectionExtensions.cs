using Kaleido.Process.AspNetCore;
using Kaleido.Process.AspNetCore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoProcessClientServiceCollectionExtensions
{
    public static IKaleidoBuilder AddProcessClient(
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
        // Multiple AddProcessClient calls each add their entry before the factory resolves.
        var routeOptions = GetOrAddRouteOptions(builder.Services);
        routeOptions[name] = new ProcessRouteOptions { RoutePrefix = routePrefix };

        builder.Services.TryAddScoped<IKaleidoProcessClientFactory, KaleidoProcessClientFactory>();

        return builder;
    }

    private static Dictionary<string, ProcessRouteOptions> GetOrAddRouteOptions(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(KaleidoProcessClientRouteOptionsMap));

        if (descriptor?.ImplementationInstance is KaleidoProcessClientRouteOptionsMap existing)
            return existing.Options;

        var map = new KaleidoProcessClientRouteOptionsMap();
        services.AddSingleton(map);
        return map.Options;
    }
}

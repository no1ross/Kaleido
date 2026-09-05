using Kaleido.Process.AspNetCore;
using Kaleido.Process.AspNetCore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoProcessClientServiceCollectionExtensions
{
    public static IKaleidoBuilder AddProcessClient(
        this IKaleidoBuilder builder,
        Action<KaleidoProcessClientOptions> configure,
        Action<KaleidoProcessClientOptions, IHttpClientBuilder>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new KaleidoProcessClientOptions();
        configure(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BaseUrl);

        var httpClientBuilder = builder.Services.AddHttpClient(options.Name, client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        configureClient?.Invoke(options, httpClientBuilder);

        // Accumulate per-name route options into a shared singleton dictionary.
        // Multiple AddProcessClient calls each add their entry before the factory resolves.
        var routeOptions = GetOrAddRouteOptions(builder.Services);
        routeOptions[options.Name] = new ProcessRouteOptions { RoutePrefix = options.RoutePrefix };

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

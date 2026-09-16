using Kaleido.Queryable.Http.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoQueryableClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers named Kaleido queryable clients from configuration.
    /// For each name, reads <c>Kaleido:Clients:&lt;Name&gt;:BaseUrl</c> (or the shared
    /// <c>Kaleido:BaseUrl</c> fallback) and calls <see cref="AddQueryableClient"/>.
    /// </summary>
    public static IKaleidoBuilder AddQueryableClients(
        this IKaleidoBuilder builder,
        params string[] clientNames)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var config = new KaleidoClientOptions();
        builder.Configuration.GetSection(KaleidoServiceOptions.SectionName).Bind(config);

        foreach (var name in clientNames)
        {
            config.Clients.TryGetValue(name, out var entry);

            var baseUrl = !string.IsNullOrWhiteSpace(entry?.BaseUrl)
                ? entry.BaseUrl
                : config.BaseUrl;

            if (string.IsNullOrWhiteSpace(baseUrl))
                continue;

            var prefix = entry?.RoutePrefix ?? name.ToLowerInvariant();

            builder.AddQueryableClient(o =>
            {
                o.Name = name;
                o.BaseUrl = baseUrl;
                o.RoutePrefix = prefix;
            });
        }

        return builder;
    }

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
        routeOptions[options.Name] = options.RoutePrefix ?? string.Empty;

        builder.Services.TryAddScoped<IKaleidoQueryableClientFactory, KaleidoQueryableClientFactory>();

        return builder;
    }

    private static Dictionary<string, string> GetOrAddRouteOptions(IServiceCollection services)
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

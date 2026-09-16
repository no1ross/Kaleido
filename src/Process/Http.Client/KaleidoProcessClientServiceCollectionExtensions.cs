using Kaleido.Process.Http.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido;

public static class KaleidoProcessClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers named Kaleido process clients from configuration.
    /// For each name, reads <c>Kaleido:Clients:&lt;Name&gt;:BaseUrl</c> (or the shared
    /// <c>Kaleido:BaseUrl</c> fallback) and calls <see cref="AddProcessClient"/>.
    /// </summary>
    public static IKaleidoBuilder AddProcessClients(
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

            builder.AddProcessClient(o =>
            {
                o.Name = name;
                o.BaseUrl = baseUrl;
                o.RoutePrefix = prefix;
            });
        }

        return builder;
    }

    public static IKaleidoBuilder AddProcessClient(
        this IKaleidoBuilder builder,
        Action<KaleidoProcessClientOptions> configure,
        Action<IHttpClientBuilder>? configureClient = null)
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

        configureClient?.Invoke(httpClientBuilder);

        // Accumulate per-name route options into a shared singleton dictionary.
        // Multiple AddProcessClient calls each add their entry before the factory resolves.
        var routeOptions = GetOrAddRouteOptions(builder.Services);
        routeOptions[options.Name] = options.RoutePrefix ?? string.Empty;

        builder.Services.TryAddScoped<IKaleidoProcessClientFactory, KaleidoProcessClientFactory>();

        return builder;
    }

    private static Dictionary<string, string> GetOrAddRouteOptions(IServiceCollection services)
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

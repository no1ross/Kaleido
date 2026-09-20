using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client;

public static class KaleidoHttpClientsServiceCollectionExtensions
{
    /// <summary>
    /// Registers named Kaleido Process and Queryable clients from configuration.
    /// Reads <c>Kaleido:Clients</c> configuration section and registers both Process and Queryable clients
    /// for each configured client name.
    /// </summary>
    public static IKaleidoBuilder AddHttpClients(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var config = new KaleidoClientOptions();
        builder.Configuration.GetSection(KaleidoServiceOptions.SectionName).Bind(config);

        foreach (var (name, entry) in config.Clients)
        {
            var baseUrl = !string.IsNullOrWhiteSpace(entry?.BaseUrl)
                ? entry.BaseUrl
                : config.BaseUrl;

            if (string.IsNullOrWhiteSpace(baseUrl))
                continue;

            var prefix = entry?.RoutePrefix ?? name.ToLowerInvariant();

            // Register Process client
            builder.AddProcessClient(o =>
            {
                o.Name = name;
                o.BaseUrl = baseUrl;
                o.RoutePrefix = prefix;
            });

            // Register Queryable client
            builder.AddQueryableClient(o =>
            {
                o.Name = name;
                o.BaseUrl = baseUrl;
                o.RoutePrefix = prefix;
            });
        }

        return builder;
    }
}

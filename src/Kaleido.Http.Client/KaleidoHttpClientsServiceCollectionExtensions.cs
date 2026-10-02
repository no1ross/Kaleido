using Kaleido.Exceptions;
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
        builder.Services.AddSingleton(config);

        foreach (var (name, entry) in config.Clients)
        {
            var baseUrl = !string.IsNullOrWhiteSpace(entry?.BaseUrl)
                ? entry.BaseUrl
                : config.BaseUrl;

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new KaleidoConfigurationException(
                    ConfigurationErrorCodes.MissingBaseUrl,
                    $"Kaleido client '{name}' has no BaseUrl configured. " +
                    $"Set 'Kaleido:Clients:{name}:BaseUrl' or the shared 'Kaleido:BaseUrl' fallback.");
            }

            var prefix = entry?.RoutePrefix ?? name.ToLowerInvariant();
            var registryTtl = entry?.RegistryTtl ?? config.RegistryTtl;
            var strictProbe = entry?.StrictRegistryProbe ?? config.StrictRegistryProbe;

            // Register Process client
            builder.AddProcessClient(o =>
            {
                o.Name = name;
                o.BaseUrl = baseUrl;
                o.RoutePrefix = prefix;
                o.RegistryTtl = registryTtl;
                o.StrictRegistryProbe = strictProbe;
            });

            // Register Queryable client
            builder.AddQueryableClient(o =>
            {
                o.Name = name;
                o.BaseUrl = baseUrl;
                o.RoutePrefix = prefix;
                o.RegistryTtl = registryTtl;
                o.StrictRegistryProbe = strictProbe;
            });
        }

        return builder;
    }
}

using Kaleido.Client;
using Kaleido.Process.AspNetCore.Client;
using Kaleido.Queryable.AspNetCore.Client;
using Microsoft.Extensions.Configuration;

namespace Kaleido;

public static class KaleidoRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Registers Kaleido downstream clients for registry aggregation, using cluster names
    /// from <c>Kaleido:Registry</c> and resolving addresses from <c>ReverseProxy:Clusters</c>.
    /// Intended for use in the Router only — services use
    /// <see cref="KaleidoClientServiceCollectionExtensions.AddKaleidoClients"/> instead.
    /// </summary>
    public static IKaleidoBuilder AddKaleidoRegistry(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new KaleidoClientOptions();
        builder.Configuration
            .GetSection(KaleidoServiceOptions.SectionName)
            .Bind(options);

        foreach (var clusterName in options.Registry)
        {
            var address = builder.Configuration[
                $"ReverseProxy:Clusters:{clusterName}:Destinations:primary:Address"];

            if (string.IsNullOrWhiteSpace(address))
                continue;

            builder.AddProcessClient(o =>
            {
                o.Name = clusterName;
                o.BaseUrl = address;
                o.RoutePrefix = clusterName;
            });

            builder.AddQueryableClient(o =>
            {
                o.Name = clusterName;
                o.BaseUrl = address;
                o.RoutePrefix = clusterName;
            });
        }

        return builder;
    }
}

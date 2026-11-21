using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Queryable.Http.Client;

/// <summary>Registers the Queryable outbound HTTP client against an explicitly hydrated Registry participant.</summary>
public static class QueryableHttpClientServiceCollectionExtensions
{
    /// <summary>Installs the named gateway client and scoped Queryable factory without starting Registry participation.</summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="options">Shared gateway settings from code, overridden by configuration.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddQueryableHttpClient(this IKaleidoBuilder builder, KaleidoClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(x => x.ServiceType == typeof(IKaleidoQueryableClientFactory)))
        {
            return builder;
        }

        builder.AddKaleidoHttpClientPipeline(options);
        builder.AddKaleidoNamedClient(KaleidoQueryableClient.ClientName);
        builder.Services.TryAddScoped<IKaleidoQueryableClientFactory, KaleidoQueryableClientFactory>();
        builder.Services.AddKeyedScoped<IKaleidoQueryableClient>(KeyedService.AnyKey,
            (provider, key) => provider.GetRequiredService<IKaleidoQueryableClientFactory>()
                .GetClient(key.RequireServiceName()));
        return builder;
    }
}

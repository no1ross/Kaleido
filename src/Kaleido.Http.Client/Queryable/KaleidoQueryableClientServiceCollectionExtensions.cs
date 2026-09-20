using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http.Client.Queryable;

public static class KaleidoQueryableClientServiceCollectionExtensions
{
    internal static IKaleidoBuilder AddQueryableClient(
        this IKaleidoBuilder builder,
        Action<KaleidoHttpClientOptions> configure,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKaleidoClient<IKaleidoQueryableClient, KaleidoQueryableClientRouteOptionsMap, KaleidoQueryableClientFactory, IKaleidoQueryableClientFactory>(
            configure,
            configureClient);

        return builder;
    }
}

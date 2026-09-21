using Kaleido.Http.Process;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http.Client.Process;

public static class KaleidoProcessClientServiceCollectionExtensions
{
    internal static IKaleidoBuilder AddProcessClient(
        this IKaleidoBuilder builder,
        Action<KaleidoHttpClientOptions> configure,
        Action<IHttpClientBuilder>? configureClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKaleidoClient<IKaleidoProcessClient, KaleidoProcessClientRouteOptionsMap, KaleidoProcessClientFactory, IKaleidoProcessClientFactory>(
            configure,
            configureClient);

        return builder;
    }
}

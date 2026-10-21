using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client;

/// <summary>
/// Adds <see cref="KaleidoCallCredentialHandler"/> to Kaleido's named clients after every handler the
/// host configured, so its <c>Authorization</c> is the one sent - only when a provider is registered;
/// without one the host's own handlers decide, as before. Other named clients are left alone.
/// </summary>
internal sealed class KaleidoCallCredentialHandlerFilter(
    KaleidoProcessorClientRouteOptionsMap processClientMap,
    KaleidoQueryableClientRouteOptionsMap queryableClientMap,
    IEnumerable<IKaleidoCallCredentialProvider> providers)
    : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return builder =>
        {
            next(builder);

            if (builder.Name is not { } name ||
                providers.LastOrDefault() is not { } provider)
            {
                return;
            }

            if (processClientMap.Options.TryGetValue(name, out var serviceName) ||
                queryableClientMap.Options.TryGetValue(name, out serviceName))
            {
                builder.AdditionalHandlers.Add(new KaleidoCallCredentialHandler(provider, name, serviceName));
            }
        };
    }
}

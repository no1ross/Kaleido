using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client;

/// <summary>
/// Adds <see cref="KaleidoCallCredentialHandler"/> as the innermost handler of every configured Kaleido
/// client's pipeline when an <see cref="IKaleidoCallCredentialProvider"/> is registered. A filter runs
/// after every handler the host configured, so the provider has the last word on Authorization.
/// </summary>
/// <remarks>
/// The handler is the client's keyed service (#498), resolved from the pipeline's own services - the
/// same way the HttpClient factory resolves any handler it adds - so the container builds it.
/// </remarks>
internal sealed class KaleidoCallCredentialHandlerFilter(
    IKaleidoClientCatalog catalog,
    IEnumerable<IKaleidoCallCredentialProvider> providers)
    : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return builder =>
        {
            next(builder);

            if (builder.Name is { } name &&
                providers.Any() &&
                catalog.Find(name) is { } client)
            {
                builder.AdditionalHandlers.Add(builder.Services.GetRequiredKeyedService<KaleidoCallCredentialHandler>(client.Name));
            }
        };
    }
}

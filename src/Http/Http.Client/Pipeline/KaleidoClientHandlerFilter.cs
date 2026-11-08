using Kaleido.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.Pipeline;

/// <summary>
/// Completes every Kaleido client's pipeline (#574): after the handlers the host configured on the client,
/// the host's Kaleido client handlers (<c>AddKaleidoClientHandler&lt;THandler&gt;()</c>, in registration
/// order), then <see cref="KaleidoOnBehalfOfGuard"/>, innermost, so it has the last word on what a call made
/// for a caller carries. Clients that are not Kaleido's are left as they are.
/// </summary>
/// <remarks>
/// The handlers are resolved from the pipeline's own services, the way the HttpClient factory resolves any
/// handler it adds, so the container builds them.
/// </remarks>
internal sealed class KaleidoClientHandlerFilter(
    IKaleidoClientCatalog catalog,
    IEnumerable<KaleidoClientHandlerRegistration> handlers)
    : IHttpMessageHandlerBuilderFilter
{
    private readonly Type[] _handlers = [.. handlers.Select(x => x.HandlerType).Distinct()];

    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return builder =>
        {
            next(builder);

            if (builder.Name is not { } name || !IsKaleidoClient(name))
            {
                return;
            }

            foreach (var handler in _handlers)
            {
                builder.AdditionalHandlers.Add((DelegatingHandler)builder.Services.GetRequiredService(handler));
            }

            // The client's own guard (#498: a keyed service per client).
            builder.AdditionalHandlers.Add(builder.Services.GetRequiredKeyedService<KaleidoOnBehalfOfGuard>(name));
        };
    }

    // A configured client, or the control registry's client (#553) when this host takes part in it.
    private bool IsKaleidoClient(string name) =>
        catalog.Find(name) is not null
        || string.Equals(name, KaleidoHttpRegistryClient.ClientName, StringComparison.Ordinal);
}

/// <summary>A host handler added to every Kaleido client (<c>AddKaleidoClientHandler&lt;THandler&gt;()</c>).</summary>
internal sealed record KaleidoClientHandlerRegistration(Type HandlerType);

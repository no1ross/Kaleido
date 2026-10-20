using System.Security.Claims;
using Kaleido.Exceptions;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client;

// Why a request is made and for whom, attached to the request where it is built (request scope:
// CorrelationHeaderStamper) and read by the innermost handler (client scope), which cannot see the
// request's services. A request without one is the service's own call.
internal sealed record KaleidoCallContext(
    KaleidoCallPurpose Purpose,
    ClaimsPrincipal? Caller,
    string? CallerAuthorization)
{
    public static readonly HttpRequestOptionsKey<KaleidoCallContext> Key = new("Kaleido.CallContext");

    private static readonly KaleidoCallContext ServiceOwn = new(KaleidoCallPurpose.ServiceOwn, Caller: null, CallerAuthorization: null);

    public static KaleidoCallContext Of(HttpRequestMessage request) =>
        request.Options.TryGetValue(Key, out var context) ? context : ServiceOwn;

    // The service's own call (a registry fetch shared by every caller): never authorized as the caller.
    public static void MarkServiceOwn(HttpRequestMessage request) =>
        request.Options.Set(Key, ServiceOwn);
}

/// <summary>
/// The innermost handler of every Kaleido named client when an <see cref="IKaleidoCallCredentialProvider"/>
/// is registered: it runs after the host's handlers and sends exactly the credential the provider
/// decides, enforcing that a call made for a caller never goes out with the host's own credentials.
/// </summary>
internal sealed class KaleidoCallCredentialHandler(
    IKaleidoCallCredentialProvider provider,
    string clientName,
    string serviceName)
    : DelegatingHandler
{
    private const string AuthorizationHeader = "Authorization";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = KaleidoCallContext.Of(request);

        // An anonymous caller stays anonymous: nothing the host configured is sent for it.
        if (context.Purpose == KaleidoCallPurpose.OnBehalfOfCaller && context.Caller is null)
        {
            request.Headers.Remove(AuthorizationHeader);
            return await base.SendAsync(request, cancellationToken);
        }

        var credential = await provider.GetCredentialAsync(
            new KaleidoCallCredentialRequest
            {
                ServiceName = serviceName,
                ClientName = clientName,
                Purpose = context.Purpose,
                Caller = context.Caller,
                CallerAuthorization = context.CallerAuthorization,
            },
            cancellationToken);

        switch (credential.Kind)
        {
            case KaleidoCallCredentialKind.Header:
                request.Headers.Remove(AuthorizationHeader);
                request.Headers.TryAddWithoutValidation(AuthorizationHeader, credential.Value);
                break;
            case KaleidoCallCredentialKind.None:
                request.Headers.Remove(AuthorizationHeader);
                break;
            case KaleidoCallCredentialKind.Configured when context.Purpose == KaleidoCallPurpose.OnBehalfOfCaller:
                throw new KaleidoConfigurationException(
                    HttpClientErrorCodes.ConfiguredCredentialForCaller,
                    $"{provider.GetType().Name} answered Configured for a call made for a caller (client '{clientName}'): a caller's call never goes out with the host's own credentials. Return a credential for the caller, or None.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

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

// ForwardCallerToken(): a caller's call sends the caller's own credential unchanged; the service's own
// calls keep what the host configured on its clients.
internal sealed class ForwardCallerTokenProvider : IKaleidoCallCredentialProvider
{
    public ValueTask<KaleidoCallCredential> GetCredentialAsync(
        KaleidoCallCredentialRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(
            request.Purpose == KaleidoCallPurpose.ServiceOwn
                ? KaleidoCallCredential.Configured
                : request.CallerAuthorization is { } authorization
                    ? KaleidoCallCredential.Header(authorization)
                    : KaleidoCallCredential.None);
    }
}

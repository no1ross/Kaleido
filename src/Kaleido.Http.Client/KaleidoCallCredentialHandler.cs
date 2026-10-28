using Kaleido.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Client;

/// <summary>
/// The innermost handler of every Kaleido named client when an <see cref="IKaleidoCallCredentialProvider"/>
/// is registered: it runs after the host's handlers and sends exactly the credential the provider
/// decides, enforcing that a call made for a caller never goes out with the host's own credentials.
/// </summary>
// A keyed service per configured client (#498): the container passes the client name, and the
// HttpClient factory adds a new instance to that client's pipeline (KaleidoCallCredentialHandlerFilter).
// The providers are taken as a collection so the registration is valid without one (a host without
// call credentials, or one that registers its provider later); the filter adds the handler only when
// one is registered, and the last registered decides, as for any single resolution.
internal sealed class KaleidoCallCredentialHandler(
    [ServiceKey] string clientName,
    IEnumerable<IKaleidoCallCredentialProvider> providers,
    IKaleidoClientCatalog catalog)
    : DelegatingHandler
{
    private readonly string _serviceName = catalog.Require(clientName).RoutePrefix;

    private readonly IKaleidoCallCredentialProvider _provider = providers.Last();

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

        var credential = await _provider.GetCredentialAsync(
            new KaleidoCallCredentialRequest
            {
                ServiceName = _serviceName,
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
                    $"{_provider.GetType().Name} answered Configured for a call made for a caller (client '{clientName}'): a caller's call never goes out with the host's own credentials. Return a credential for the caller, or None.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

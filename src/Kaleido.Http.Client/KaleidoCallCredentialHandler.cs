using Kaleido.Exceptions;

namespace Kaleido.Http.Client;

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

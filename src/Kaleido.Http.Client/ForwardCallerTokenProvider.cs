namespace Kaleido.Http.Client;

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

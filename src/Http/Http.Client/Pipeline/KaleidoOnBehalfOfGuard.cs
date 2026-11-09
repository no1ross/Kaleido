using Kaleido.Exceptions;

namespace Kaleido.Http.Client.Pipeline;

/// <summary>
/// The innermost handler of every Kaleido client (#574): it runs after the host's own handlers and
/// enforces what a call made for a caller carries (<see cref="KaleidoCallPurpose.OnBehalfOfCaller"/>):
/// nothing for an anonymous caller; in <see cref="KaleidoOnBehalfOfCalls.Forward"/> exactly the caller's
/// token; in <see cref="KaleidoOnBehalfOfCalls.Exchange"/> a token the host's handler set that is not the
/// inbound one, or the call is refused before it is sent. The service's own calls keep what the host's
/// handlers set.
/// </summary>
internal sealed class KaleidoOnBehalfOfGuard(KaleidoClientOptions? options = null)
    : DelegatingHandler
{
    private const string AuthorizationHeader = "Authorization";

    private readonly KaleidoOnBehalfOfCalls _mode = options?.OnBehalfOfCalls ?? KaleidoOnBehalfOfCalls.Forward;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var call = KaleidoCallContext.Of(request);

        if (call.Purpose == KaleidoCallPurpose.OnBehalfOfCaller)
        {
            Enforce(request, call);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private void Enforce(HttpRequestMessage request, KaleidoCallContext call)
    {
        // An anonymous caller stays anonymous: nothing the host's handlers set is sent for it.
        if (call.Caller is null)
        {
            request.Headers.Remove(AuthorizationHeader);
            return;
        }

        if (_mode == KaleidoOnBehalfOfCalls.Forward)
        {
            request.Headers.Remove(AuthorizationHeader);

            if (call.CallerAuthorization is { } inbound)
            {
                request.Headers.TryAddWithoutValidation(AuthorizationHeader, inbound);
            }

            return;
        }

        var sent = request.Headers.TryGetValues(AuthorizationHeader, out var values)
            ? string.Join(',', values)
            : null;

        if (string.IsNullOrWhiteSpace(sent) || string.Equals(sent, call.CallerAuthorization, StringComparison.Ordinal))
        {
            throw new KaleidoConfigurationException(
                HttpClientErrorCodes.CallerTokenNotExchanged,
                $"Kaleido:OnBehalfOfCalls is Exchange, but the call to {request.RequestUri?.GetLeftPart(UriPartial.Authority)} for the caller " +
                (string.IsNullOrWhiteSpace(sent) ? "carries no token" : "carries the caller's inbound token unchanged") +
                ": a handler added with AddKaleidoClientHandler<THandler>() must set the caller's exchanged token (KaleidoCallContext.CallerAuthorization is the one to exchange).");
        }
    }
}

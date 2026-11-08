using Kaleido.Client.Calls;
using Kaleido.Http.Client.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Guide.Examples.Callers;

// Your identity provider's token exchange (RFC 8693, or On-Behalf-Of): an assumption of this example.
// A library such as IdentityModel (RequestTokenExchangeTokenAsync) or Microsoft.Identity.Web does the request.
public interface ITokenExchange
{
    Task<string> ExchangeAsync(string callerAuthorization, CancellationToken cancellationToken);
}

// snippet: callers-credentials
// A standard DelegatingHandler on the Kaleido clients: for a call made for a caller, the caller's token
// exchanged for one the services accept. Kaleido tells it which call it is; the service's own calls are
// left to the host's other handlers (e.g. a client-credentials token).
public sealed class TokenExchangeHandler(ITokenExchange exchange) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var call = KaleidoCallContext.Of(request);

        if (call.Purpose == KaleidoCallPurpose.OnBehalfOfCaller && call.CallerAuthorization is { } authorization)
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", await exchange.ExchangeAsync(authorization, cancellationToken));
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
// end snippet

public static class CallCredentialsRegistration
{
    public static void AddWithTokenExchange(IServiceCollection services, IConfiguration configuration)
    {
        // snippet: callers-credentials-registration
        // Kaleido:OnBehalfOfCalls = Exchange in configuration: a caller's call never carries the inbound token.
        services.AddKaleido(configuration, new() { ServiceName = "orders", Assemblies = [typeof(TokenExchangeHandler).Assembly] })
            .AddHttpClients()
            .AddKaleidoClientHandler<TokenExchangeHandler>();
        // end snippet
    }
}

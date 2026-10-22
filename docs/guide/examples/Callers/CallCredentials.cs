using Kaleido.Http;
using Kaleido.Http.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Guide.Examples.Callers;

// Your identity provider's token exchange (on-behalf-of): an assumption of this example.
public interface ITokenExchange
{
    Task<string> ExchangeAsync(string callerAuthorization, string audience, CancellationToken cancellationToken);
}

// snippet: callers-credentials
// Decides the credential of every call this service makes to another Kaleido service.
public sealed class OnBehalfOfCredentials(ITokenExchange exchange) : IKaleidoCallCredentialProvider
{
    public async ValueTask<KaleidoCallCredential> GetCredentialAsync(
        KaleidoCallCredentialRequest request,
        CancellationToken cancellationToken)
    {
        // A call made for a caller: exchange the caller's token for one the target service accepts.
        if (request.Purpose == KaleidoCallPurpose.OnBehalfOfCaller && request.CallerAuthorization is { } authorization)
        {
            return KaleidoCallCredential.Header(await exchange.ExchangeAsync(authorization, request.ServiceName, cancellationToken));
        }

        // The service's own calls (registry refresh, health probe): its configured credential.
        return KaleidoCallCredential.Configured;
    }
}
// end snippet

public static class CallCredentialsRegistration
{
    public static void AddWithOnBehalfOf(IServiceCollection services, IConfiguration configuration)
    {
        // snippet: callers-credentials-registration
        services.AddKaleido(configuration, o =>
            {
                o.ServiceName = "orders";
                o.Assemblies = [typeof(OnBehalfOfCredentials).Assembly];
            })
            .AddHttp()
            .AddHttpClients()
            .AddCallCredentials<OnBehalfOfCredentials>();   // or .ForwardCallerToken(): the caller's token, unchanged
        // end snippet
    }
}

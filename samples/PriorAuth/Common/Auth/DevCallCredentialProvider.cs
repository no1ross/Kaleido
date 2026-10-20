using System.Security.Claims;
using Kaleido.Http.Client;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Auth;

/// <summary>
/// The sample's <see cref="IKaleidoCallCredentialProvider"/> for its services (a dev stand-in for a
/// real token exchange). A call made for a caller gets an on-behalf-of token: the user's name and
/// the user's own roles, unchanged, plus this service as the <see cref="DevAuthClaims.Actor"/>, so
/// downstream ownership/audit records the real user while internal-only capabilities
/// (<see cref="DevAuthPolicies.InternalCaller"/>) see the hop. The service's own calls get a
/// service-to-service token. Kaleido never asks for an anonymous caller: it stays anonymous.
/// </summary>
public sealed class DevCallCredentialProvider(
    IConfiguration configuration)
    : IKaleidoCallCredentialProvider
{
    public ValueTask<KaleidoCallCredential> GetCredentialAsync(
        KaleidoCallCredentialRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key =
            configuration[DevTokenIssuer.AuthKeyConfigName]
            ?? DevTokenIssuer.DefaultKey;

        var serviceName =
            configuration["Kaleido:ServiceName"] ?? "unknown";

        var token =
            request.Caller is { } user
                ? DevTokenIssuer.Issue(
                    user.Identity?.Name ?? "unknown",
                    [.. user.Claims
                            .Where(c => c.Type == ClaimTypes.Role)
                            .Select(c => c.Value)],
                    key,
                    actor: serviceName)
                : DevTokenIssuer.IssueServiceToken(serviceName, key);

        return ValueTask.FromResult(KaleidoCallCredential.Header($"Bearer {token}"));
    }
}

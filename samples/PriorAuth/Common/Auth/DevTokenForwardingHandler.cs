using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace Kaleido.Samples.PriorAuth.Auth;

/// <summary>
/// Delegating handler for outbound Kaleido client calls. When an inbound user
/// is authenticated it mints an on-behalf-of token — the user's name plus the
/// union of the user's roles and the calling service's "internal" role — so
/// downstream ownership/audit records the real user while service-only
/// capabilities (e.g. <c>Roles = "internal"</c> views) stay reachable. With no
/// inbound user it mints a service-to-service <c>svc-{name}</c> token.
/// </summary>
public sealed class DevTokenForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var key =
            configuration[DevTokenIssuer.AuthKeyConfigName]
            ?? DevTokenIssuer.DefaultKey;

        var serviceName =
            configuration["Kaleido:ServiceName"] ?? "unknown";

        var user = httpContextAccessor.HttpContext?.User;

        var token =
            user?.Identity?.IsAuthenticated == true
                ? DevTokenIssuer.Issue(
                    user.Identity.Name ?? "unknown",
                    [.. user.Claims
                            .Where(c => c.Type == ClaimTypes.Role)
                            .Select(c => c.Value)
                            .Append("internal")
                            .Append(serviceName)
                            .Distinct(StringComparer.OrdinalIgnoreCase)],
                    key)
                : DevTokenIssuer.IssueServiceToken(serviceName, key);

        request.Headers.TryAddWithoutValidation(
            HeaderNames.Authorization, $"Bearer {token}");

        return base.SendAsync(request, cancellationToken);
    }
}

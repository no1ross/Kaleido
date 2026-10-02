using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace Kaleido.Samples.PriorAuth.Auth;

/// <summary>
/// Delegating handler for outbound Kaleido client calls: forwards the inbound
/// request's <c>Authorization</c> header when present (user token flows
/// through), otherwise mints a service-to-service <c>svc-{name}</c> token
/// carrying the "internal" role.
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
        var inbound =
            httpContextAccessor.HttpContext?.Request
                .Headers.Authorization.ToString();

        if (!string.IsNullOrWhiteSpace(inbound))
        {
            request.Headers.TryAddWithoutValidation(
                HeaderNames.Authorization, inbound);
        }
        else
        {
            var key =
                configuration[DevTokenIssuer.AuthKeyConfigName]
                ?? DevTokenIssuer.DefaultKey;

            var serviceName =
                configuration["Kaleido:ServiceName"] ?? "unknown";

            request.Headers.TryAddWithoutValidation(
                HeaderNames.Authorization,
                $"Bearer {DevTokenIssuer.IssueServiceToken(serviceName, key)}");
        }

        return base.SendAsync(request, cancellationToken);
    }
}

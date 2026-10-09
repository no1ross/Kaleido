using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Kaleido.Http.Mcp.Http;

/// <summary>
/// Forwards the MCP caller's identity to Kaleido: the inbound <c>Authorization</c> header is
/// copied unchanged onto every outbound call, so Kaleido authorizes the agent's user exactly as
/// it would a direct caller (ZeroTrust, per-caller registry filtering, ownership). The inbound
/// <c>X-Kaleido-Request-Id</c> is forwarded for correlation when present.
/// </summary>
/// <remarks>
/// MCP tool calls run on the execution context of the HTTP request that carried them (the SDK's
/// default), so <see cref="IHttpContextAccessor"/> yields that request.
/// </remarks>
internal sealed class KaleidoForwardingHandler(
    IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (httpContextAccessor.HttpContext?.Request.Headers is { } inbound)
        {
            var authorization = inbound[HeaderNames.Authorization].ToString();

            if (!string.IsNullOrWhiteSpace(authorization))
            {
                request.Headers.Remove(HeaderNames.Authorization);
                request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, authorization);
            }

            var requestId = inbound[KaleidoCorrelationHeaders.RequestId].ToString();

            if (!string.IsNullOrWhiteSpace(requestId) &&
                !request.Headers.Contains(KaleidoCorrelationHeaders.RequestId))
            {
                request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, requestId.Sanitize());
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}

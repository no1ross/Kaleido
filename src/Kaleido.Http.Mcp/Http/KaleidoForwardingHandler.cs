using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Kaleido.Http.Mcp.Http;

/// <summary>
/// Forwards the MCP caller's identity and correlation to Kaleido: the inbound <c>Authorization</c>
/// header is copied unchanged onto every outbound call, so Kaleido authorizes the agent's user
/// exactly as it would a direct caller (ZeroTrust, per-caller registry filtering, ownership), and
/// every call carries the tool call's <c>X-Kaleido-Request-Id</c>.
/// </summary>
/// <remarks>
/// MCP tool calls run on the execution context of the HTTP request that carried them (the SDK's
/// default), so <see cref="IHttpContextAccessor"/> yields that request.
/// </remarks>
internal sealed class KaleidoForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    IMcpRequestIdAccessor requestIdAccessor)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authorization =
            httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();

        if (!string.IsNullOrWhiteSpace(authorization))
        {
            request.Headers.Remove(HeaderNames.Authorization);
            request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, authorization);
        }

        if (!request.Headers.Contains(KaleidoCorrelationHeaders.RequestId))
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, requestIdAccessor.Current);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

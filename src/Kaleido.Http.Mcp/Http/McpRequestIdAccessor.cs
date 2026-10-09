using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.Mcp.Http;

/// <summary>
/// The <c>X-Kaleido-Request-Id</c> of the current MCP request (in the stateless transport: one tool
/// call or tool listing). The inbound header is reused when the client sent one, otherwise one is
/// generated, once per request, so every Kaleido call the tool makes, the adapter's telemetry and the
/// tool result all carry the same id.
/// </summary>
internal interface IMcpRequestIdAccessor
{
    string Current { get; }
}

internal sealed class McpRequestIdAccessor(
    IHttpContextAccessor httpContextAccessor)
    : IMcpRequestIdAccessor
{
    private const string ItemKey = "Kaleido.Http.Mcp.RequestId";

    public string Current
    {
        get
        {
            if (httpContextAccessor.HttpContext is not { } context)
            {
                return NewId();
            }

            if (context.Items.TryGetValue(ItemKey, out var stored) && stored is string existing)
            {
                return existing;
            }

            var inbound = context.Request.Headers[KaleidoCorrelationHeaders.RequestId].ToString();

            var requestId = inbound.Sanitize() is { Length: > 0 } sanitized
                ? sanitized
                : NewId();

            context.Items[ItemKey] = requestId;

            return requestId;
        }
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
}

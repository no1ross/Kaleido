using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaleido.Http.Mcp;

public static class KaleidoMcpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Kaleido MCP endpoint (streamable HTTP) at <paramref name="pattern"/>. Agents connect
    /// here with an <c>Authorization: Bearer …</c> header; the tools they see are filtered by that
    /// caller's Kaleido access.
    /// </summary>
    public static IEndpointConventionBuilder MapKaleidoMcp(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints.MapMcp(pattern);
    }
}

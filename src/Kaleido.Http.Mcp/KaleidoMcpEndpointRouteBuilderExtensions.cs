using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaleido.Http.Mcp;

/// <summary>
/// Maps the Kaleido MCP endpoint registered by <c>AddMcp()</c>; the entry point is
/// <see cref="MapKaleidoMcp"/>.
/// </summary>
public static class KaleidoMcpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Kaleido MCP endpoint (streamable HTTP) at <paramref name="pattern"/>. Agents connect
    /// here with an <c>Authorization: Bearer …</c> header; the tools they see are filtered by that
    /// caller's Kaleido access, and every tool runs as that caller.
    /// </summary>
    /// <param name="endpoints">The host's endpoints, after <c>MapKaleidoHttp(...)</c>.</param>
    /// <param name="pattern">The MCP endpoint route; <c>/mcp</c> by default.</param>
    /// <returns>The endpoint convention builder (e.g. to add authorization or rate limiting).</returns>
    /// <remarks>
    /// <para>
    /// The tools are the registry <c>MapKaleidoHttp</c> maps; until it is mapped the adapter lists
    /// no tools. Requires <c>AddMcp()</c>.
    /// </para>
    /// <para>
    /// For sign-in from MCP clients, call <c>.RequireAuthorization()</c> on the result and add the MCP
    /// SDK's authentication scheme (<c>AddAuthentication().AddMcp(...)</c>) as the challenge scheme: a
    /// caller without a token then gets 401 with the protected-resource metadata that names the
    /// identity provider (see the README, "Sign-in from MCP clients").
    /// </para>
    /// </remarks>
    public static IEndpointConventionBuilder MapKaleidoMcp(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints.MapMcp(pattern);
    }
}

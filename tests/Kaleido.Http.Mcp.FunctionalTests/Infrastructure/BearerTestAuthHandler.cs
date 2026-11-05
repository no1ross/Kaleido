using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// Test authentication from <c>Authorization: Bearer {user}|{role,role}</c>, optionally followed by
/// <c>|exchanged</c>: the token the MCP host's exchange (<see cref="TestTokenExchangeHandler"/>) sends for the
/// same user, so the Kaleido host sees the agent's user without receiving the agent's token (#574).
/// </summary>
internal sealed class BearerTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "bearer-test";

    internal static string Token(string user, params string[] roles) =>
        $"Bearer {user}|{string.Join(",", roles)}";

    /// <summary>The token the MCP host exchanges <paramref name="token"/> for: the same user and roles, a different token.</summary>
    internal static string Exchanged(string token) =>
        $"{token}|exchanged";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var parts = header["Bearer ".Length..].Split('|');

        var claims = new List<Claim> { new(ClaimTypes.Name, parts[0]) };

        if (parts.Length > 1)
        {
            claims.AddRange(
                parts[1]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => new Claim(ClaimTypes.Role, x)));
        }

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)),
                    SchemeName)));
    }
}

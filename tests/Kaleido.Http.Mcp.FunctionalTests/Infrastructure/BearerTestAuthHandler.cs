using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// Test authentication from <c>Authorization: Bearer {user}|{role,role}</c>. The MCP adapter
/// forwards exactly this header, so the Kaleido host sees the agent's user.
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

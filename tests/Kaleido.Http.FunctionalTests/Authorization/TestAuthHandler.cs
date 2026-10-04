using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.FunctionalTests.Authorization;

/// <summary>
/// Header-driven test authentication scheme. <c>X-Test-User</c> +
/// comma-delimited <c>X-Test-Roles</c> produce an authenticated principal;
/// no <c>X-Test-User</c> → anonymous.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options,
        logger,
        encoder)
{
    internal const string SchemeName = "test";
    internal const string UserHeader = "X-Test-User";
    internal const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user)
            || string.IsNullOrWhiteSpace(user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.ToString())
        };

        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
        {
            claims.AddRange(
                roles.ToString()
                    .Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Select(r => new Claim(ClaimTypes.Role, r)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(
                    new ClaimsPrincipal(identity),
                    SchemeName)));
    }
}

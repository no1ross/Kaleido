using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaleido.Http.FunctionalTests.Authorization;

/// <summary>
/// Header-driven test authentication scheme. <c>X-Test-User</c> +
/// comma-delimited <c>X-Test-Roles</c> (and an optional <c>X-Test-Subject</c>, the <c>sub</c> claim)
/// produce an authenticated principal; an optional <c>X-Test-Role-Claim</c> makes the identity's
/// <c>RoleClaimType</c> (and the roles' claim type) that value, as a JWT handler keeping "roles" does;
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
    internal const string SubjectHeader = "X-Test-Subject";
    internal const string RoleClaimHeader = "X-Test-Role-Claim";
    internal const string ChallengeHeaderValue = "Test realm=\"kaleido\"";

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = ChallengeHeaderValue;
        return base.HandleChallengeAsync(properties);
    }

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

        if (Request.Headers.TryGetValue(SubjectHeader, out var subject))
        {
            claims.Add(new Claim("sub", subject.ToString()));
        }

        var roleClaimType =
            Request.Headers.TryGetValue(RoleClaimHeader, out var roleClaim) && !string.IsNullOrWhiteSpace(roleClaim)
                ? roleClaim.ToString()
                : ClaimTypes.Role;

        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
        {
            claims.AddRange(
                roles.ToString()
                    .Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .Select(r => new Claim(roleClaimType, r)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.Name, roleClaimType);

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(
                    new ClaimsPrincipal(identity),
                    SchemeName)));
    }
}

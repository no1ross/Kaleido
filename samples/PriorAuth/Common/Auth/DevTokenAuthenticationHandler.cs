using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kaleido.Samples.PriorAuth.Auth;

/// <summary>
/// Authentication handler for the sample's dev-token scheme. Reads
/// <c>Authorization: Bearer dev.…</c>, validates the HMAC signature and
/// expiry, and populates <c>HttpContext.User</c> with name + role claims.
/// </summary>
public sealed class DevTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    public const string SchemeName = "DevAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Context.Request.Headers.Authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var key =
            configuration[DevTokenIssuer.AuthKeyConfigName]
            ?? DevTokenIssuer.DefaultKey;

        if (!DevTokenIssuer.TryValidate(
                header["Bearer ".Length..].Trim(),
                key,
                out var identity)
            || identity is null)
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Invalid dev token."));
        }

        var claims =
            identity.Roles
                .Select(r => new Claim(ClaimTypes.Role, r))
                .Append(new Claim(ClaimTypes.Name, identity.Name));

        var principal =
            new ClaimsPrincipal(
                new ClaimsIdentity(claims, SchemeName));

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(principal, SchemeName)));
    }
}

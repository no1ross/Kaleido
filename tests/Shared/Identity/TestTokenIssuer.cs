using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kaleido.Testing.Identity;

/// <summary>
/// A test identity provider for functional tests (#574): it signs real JWTs with an in-memory RSA key,
/// which <see cref="TestJwtAuthentication"/> hosts validate with ASP.NET's JWT bearer handler, as
/// they would a provider's tokens. User tokens carry <c>sub</c>, the name, <c>roles</c> and the app
/// the user signed in through (<c>azp</c>); app-only tokens (client credentials) carry the client as
/// <c>azp</c> and <c>client_id</c>, and no user.
/// </summary>
internal static class TestTokenIssuer
{
    /// <summary>The issuer every test token names and every test host trusts.</summary>
    public const string Issuer = "https://idp.kaleido.test";

    /// <summary>The platform audience: every Kaleido service accepts it.</summary>
    public const string PlatformAudience = "kaleido-platform";

    /// <summary>The claim the roles travel in, as most providers issue them.</summary>
    public const string RoleClaimType = "roles";

    /// <summary>The claim the user's name travels in (OpenID Connect).</summary>
    public const string NameClaimType = "preferred_username";

    /// <summary>The app a user signs in through unless a test names another.</summary>
    public const string DefaultUserApp = "test-ui";

    private static readonly RSA Rsa = RSA.Create(2048);

    private static readonly JsonWebTokenHandler Handler = new();

    /// <summary>The key test tokens are signed with; test hosts trust it.</summary>
    public static SecurityKey SigningKey { get; } = new RsaSecurityKey(Rsa) { KeyId = "kaleido-test" };

    /// <summary>A user's access token: <c>Bearer</c> value without the scheme.</summary>
    public static string User(
        string name,
        string[]? roles = null,
        string app = DefaultUserApp,
        string audience = PlatformAudience,
        TimeSpan? lifetime = null) =>
        Issue(
            new Dictionary<string, object>
            {
                ["sub"] = $"user-{name}",
                [NameClaimType] = name,
                ["azp"] = app,
                [RoleClaimType] = roles ?? [],
            },
            audience,
            lifetime,
            SigningKey);

    /// <summary>An app-only access token (client credentials): the client, its roles, no user.</summary>
    public static string App(
        string clientId,
        string[]? roles = null,
        string audience = PlatformAudience) =>
        Issue(
            new Dictionary<string, object>
            {
                ["sub"] = $"service-account-{clientId}",
                ["azp"] = clientId,
                ["client_id"] = clientId,
                [RoleClaimType] = roles ?? [],
            },
            audience,
            lifetime: null,
            SigningKey);

    /// <summary>A user token signed with a key no test host trusts: a forged token.</summary>
    public static string Forged(string name, string[]? roles = null)
    {
        using var rsa = RSA.Create(2048);

        return Issue(
            new Dictionary<string, object>
            {
                ["sub"] = $"user-{name}",
                [NameClaimType] = name,
                ["azp"] = DefaultUserApp,
                [RoleClaimType] = roles ?? [],
            },
            PlatformAudience,
            lifetime: null,
            new RsaSecurityKey(rsa) { KeyId = "forged" });
    }

    private static string Issue(
        Dictionary<string, object> claims,
        string audience,
        TimeSpan? lifetime,
        SecurityKey key)
    {
        var now = DateTime.UtcNow;

        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + (lifetime ?? TimeSpan.FromMinutes(5)),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });
    }
}

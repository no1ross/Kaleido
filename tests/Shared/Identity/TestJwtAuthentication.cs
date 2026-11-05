using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Kaleido.Testing.Identity;

/// <summary>
/// Test hosts authenticate the way a production Kaleido service does (#574): ASP.NET's JWT bearer
/// handler validating the issuer, the audience, the signature and the lifetime of
/// <see cref="TestTokenIssuer"/>'s tokens, with the claim names kept as issued
/// (<c>MapInboundClaims = false</c>) and <c>roles</c> as the role claim.
/// </summary>
internal static class TestJwtAuthentication
{
    /// <summary>Adds JWT bearer authentication trusting the test issuer, for <paramref name="audience"/>.</summary>
    public static IServiceCollection AddTestJwtBearer(
        this IServiceCollection services,
        string audience = TestTokenIssuer.PlatformAudience)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = TestTokenIssuer.Issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = TestTokenIssuer.SigningKey,
                    NameClaimType = TestTokenIssuer.NameClaimType,
                    RoleClaimType = TestTokenIssuer.RoleClaimType,
                    ClockSkew = TimeSpan.Zero,
                };
            });

        return services;
    }

    /// <summary>The <c>Authorization</c> value for <paramref name="token"/>.</summary>
    public static AuthenticationHeaderValue Bearer(string token) =>
        new(JwtBearerDefaults.AuthenticationScheme, token);
}

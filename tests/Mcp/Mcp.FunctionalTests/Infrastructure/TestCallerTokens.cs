using Microsoft.IdentityModel.JsonWebTokens;

namespace Kaleido.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// Signed test user credentials and assertions about the MCP host's distinct exchanged token,
/// validated by real JWT bearer authentication on both TestServer hosts (#574).
/// </summary>
internal static class TestCallerTokens
{
    internal static string Token(string user, params string[] roles) =>
        TestJwtAuthentication.Bearer(TestTokenIssuer.User(user, roles)).ToString();

    /// <summary>An outbound token for the same user and roles, issued to the MCP exchange app rather than copied from the caller.</summary>
    internal static bool IsExchangedFor(string authorization, string inbound)
    {
        if (string.IsNullOrWhiteSpace(authorization) || string.Equals(authorization, inbound, StringComparison.Ordinal))
        {
            return false;
        }

        var original = Claims(inbound);
        var exchanged = Claims(authorization);
        return Claim(exchanged, TestTokenIssuer.NameClaimType) == Claim(original, TestTokenIssuer.NameClaimType)
            && Claim(exchanged, "azp") == "mcp-exchange"
            && exchanged.Where(x => x.Type == TestTokenIssuer.RoleClaimType).Select(x => x.Value)
                .ToHashSet(StringComparer.Ordinal).SetEquals(original.Where(x => x.Type == TestTokenIssuer.RoleClaimType).Select(x => x.Value));
    }

    internal static bool IsServiceOwn(string authorization) =>
        !string.IsNullOrWhiteSpace(authorization) && Claims(authorization).Any(x => x.Type == "client_id");

    private static IReadOnlyList<System.Security.Claims.Claim> Claims(string authorization) =>
        authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? [.. new JsonWebToken(authorization["Bearer ".Length..]).Claims]
            : [];

    private static string? Claim(IEnumerable<System.Security.Claims.Claim> claims, string type) =>
        claims.FirstOrDefault(x => x.Type == type)?.Value;
}

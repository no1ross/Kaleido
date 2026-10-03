using System.Security.Cryptography;
using System.Text;

namespace Kaleido.Samples.PriorAuth.Auth;

/// <summary>
/// Issues and validates HMAC-signed dev tokens for the sample. This is a
/// demo-only stand-in for a real identity provider — tokens carry a
/// <c>name|role1,role2|actor|expiryUtc</c> payload plus an HMACSHA256
/// signature, both base64url-encoded: <c>dev.&lt;payload&gt;.&lt;sig&gt;</c>.
/// </summary>
/// <remarks>
/// Roles describe the <b>user</b>; the optional actor names the <b>service</b>
/// making a service-to-service call (on behalf of the user, or on its own).
/// Users never carry an "internal" role — internal-only capabilities check
/// for the actor via <see cref="DevAuthPolicies.InternalCaller"/>.
/// </remarks>
public static class DevTokenIssuer
{
    /// <summary>Config key holding the shared HMAC secret.</summary>
    public const string AuthKeyConfigName = "PriorAuth:DevAuthKey";

    /// <summary>
    /// Fallback dev-only secret when the config key is absent. Never use in
    /// production — this exists so the sample runs with zero configuration.
    /// </summary>
    public const string DefaultKey =
        "priorauth-sample-dev-key-do-not-use-in-production";

    /// <summary>
    /// App roles granted to a service principal (e.g. the router aggregating
    /// registries) so it sees every downstream capability — the equivalent of
    /// app-role assignments on a daemon identity in a real IdP.
    /// </summary>
    private static readonly string[] ServiceAppRoles = ["radiology", "admin"];

    private static readonly TimeSpan DefaultLifetime =
        TimeSpan.FromHours(8);

    public static string Issue(
        string name,
        IReadOnlyCollection<string> roles,
        string key,
        string? actor = null,
        TimeSpan? lifetime = null)
    {
        var expiry =
            DateTimeOffset.UtcNow
                .Add(lifetime ?? DefaultLifetime)
                .ToUnixTimeSeconds();

        var payload =
            Base64UrlEncode(
                $"{name}|{string.Join(",", roles)}|{actor}|{expiry}");

        return $"dev.{payload}.{Sign(payload, key)}";
    }

    /// <summary>
    /// Issues a service-to-service token with no user: name
    /// <c>svc-{serviceName}</c>, the service's app roles, and the service as
    /// actor.
    /// </summary>
    public static string IssueServiceToken(
        string serviceName,
        string key) =>
        Issue($"svc-{serviceName}", ServiceAppRoles, key, actor: serviceName);

    public static bool TryValidate(
        string token,
        string key,
        out DevTokenIdentity? identity)
    {
        identity = null;

        var parts = token.Split('.');
        if (parts.Length != 3
            || !string.Equals(parts[0], "dev", StringComparison.Ordinal))
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Sign(parts[1], key)),
                Encoding.UTF8.GetBytes(parts[2])))
        {
            return false;
        }

        var fields =
            Encoding.UTF8.GetString(Base64UrlDecode(parts[1]))
                .Split('|');

        if (fields.Length != 4
            || !long.TryParse(fields[3], out var expiry)
            || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry)
        {
            return false;
        }

        identity = new DevTokenIdentity(
            fields[0],
            fields[1].Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries),
            string.IsNullOrWhiteSpace(fields[2]) ? null : fields[2]);

        return true;
    }

    private static string Sign(string payload, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Base64UrlEncode(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string Base64UrlEncode(string value) =>
        Base64UrlEncode(Encoding.UTF8.GetBytes(value));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        return Convert.FromBase64String(base64);
    }
}

/// <summary>The identity carried by a validated dev token.</summary>
/// <param name="Name">The user (or <c>svc-{service}</c> for service tokens).</param>
/// <param name="Roles">The user's roles (or a service principal's app roles).</param>
/// <param name="Actor">The calling service on service-to-service hops; null for direct user calls.</param>
public sealed record DevTokenIdentity(
    string Name,
    string[] Roles,
    string? Actor);

using System.Net;

namespace Kaleido.Http.Client;

/// <summary>
/// Validates URLs returned by remote registries before the client follows them.
/// A compromised or misconfigured remote could otherwise point the client at
/// arbitrary schemes or hosts (SSRF).
/// Relative paths are always accepted — they resolve against the client's own
/// BaseAddress. Absolute URLs must be http(s) and target the same host.
/// </summary>
internal static class RegistryUrlValidator
{
    public static string RequireValidRegistryUrl(
        this string? url,
        string fieldName,
        Uri? baseAddress)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        {
            throw Invalid(url, fieldName);
        }

        if (!uri.IsAbsoluteUri)
        {
            return url;
        }

        var isHttp =
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) ||
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

        var sameHost =
            baseAddress is null ||
            string.Equals(uri.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase);

        if (!isHttp || !sameHost)
        {
            throw Invalid(url, fieldName);
        }

        return url;
    }

    private static KaleidoHttpClientException Invalid(string? url, string fieldName) =>
        new(
            HttpClientErrorCodes.InvalidRegistryUrl,
            $"Registry-supplied URL for '{fieldName}' is not a valid same-host http(s) URL or relative path: '{url}'.",
            HttpStatusCode.InternalServerError);
}

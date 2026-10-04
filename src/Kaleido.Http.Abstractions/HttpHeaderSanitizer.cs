namespace Kaleido.Http;

/// <summary>
/// Sanitizes HTTP header field values per RFC 7230:
/// only printable US-ASCII characters (0x20–0x7E) are permitted,
/// and values are capped at <see cref="MaxLength"/> to prevent oversized headers.
/// </summary>
public static class HttpHeaderSanitizerExtensions
{
    /// <summary>Maximum permitted header value length. Values exceeding this are truncated.</summary>
    public const int MaxLength = 256;

    /// <summary>
    /// Strips non-printable-ASCII characters, trims whitespace, truncates to
    /// <see cref="MaxLength"/> characters, and returns <c>null</c> if the result is empty.
    /// </summary>
    public static string? Sanitize(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (IsClean(value))
        {
            return value;
        }

        var sanitized = new string(
            value.Where(c => c >= 0x20 && c <= 0x7E).ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return null;
        }

        return sanitized.Length <= MaxLength
            ? sanitized
            : sanitized[..MaxLength];
    }

    private static bool IsClean(string value)
    {
        if (value.Length > MaxLength || value[0] == ' ' || value[^1] == ' ')
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c < 0x20 || c > 0x7E)
            {
                return false;
            }
        }

        return true;
    }
}

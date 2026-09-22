namespace Kaleido.Http;

/// <summary>
/// Sanitizes HTTP header field values per RFC 7230:
/// only printable US-ASCII characters (0x20–0x7E) are permitted,
/// and values are capped at <see cref="MaxLength"/> to prevent oversized headers.
/// </summary>
public static class HttpHeaderSanitizer
{
    /// <summary>Maximum permitted header value length. Values exceeding this are truncated.</summary>
    public const int MaxLength = 256;

    /// <summary>
    /// Strips non-printable-ASCII characters, trims whitespace, truncates to
    /// <see cref="MaxLength"/> characters, and returns <c>null</c> if the result is empty.
    /// </summary>
    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var sanitized = new string(
            value.Where(c => c >= 0x20 && c <= 0x7E).ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
            return null;

        return sanitized.Length <= MaxLength
            ? sanitized
            : sanitized[..MaxLength];
    }
}

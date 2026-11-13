using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.Server;

/// <summary>HTTP server settings for interpreting inbound correlation identity.</summary>
/// <remarks>The <c>Kaleido:Http</c> configuration section is overlaid on code-provided settings.
/// <see cref="TrustCorrelationIdentity"/> can only be set in code.</remarks>
public sealed record KaleidoHttpServerOptions
{
    /// <summary>The HTTP server configuration section.</summary>
    public const string SectionName = "Kaleido:Http";

    /// <summary>
    /// Determines whether identity-bearing correlation headers are trusted. When absent, headers
    /// are trusted without an authentication scheme, or for an authenticated caller.
    /// </summary>
    public Func<HttpContext, bool>? TrustCorrelationIdentity { get; init; }
}

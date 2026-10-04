using Microsoft.AspNetCore.Http;
namespace Kaleido.Http;

/// <summary>
/// Options for the Kaleido HTTP transport. Kaleido performs no authentication
/// itself — these options shape how inbound correlation headers are
/// interpreted against the host's authentication middleware. Capability
/// authorization is controlled by
/// <see cref="KaleidoServiceOptions.AuthorizationMode"/>.
/// </summary>
public sealed class KaleidoHttpOptions
{
    /// <summary>
    /// Decides whether identity-bearing correlation headers
    /// (<c>X-Kaleido-Request-Id</c>, <c>X-Kaleido-Source-Processor</c>,
    /// <c>X-Kaleido-Step-Name</c>, <c>X-Kaleido-Processor-Instance-Id</c>)
    /// are honored for a request. <c>X-Kaleido-Process-Id</c> is always
    /// honored — it is a resumable process handle, not an identity claim.
    /// <para>
    /// When <c>null</c> (default): trusted when the host has no
    /// authentication infrastructure registered (nothing to check
    /// against — preserves pre-auth behavior), otherwise requires an
    /// authenticated caller.
    /// </para>
    /// </summary>
    public Func<HttpContext, bool>? TrustCorrelationIdentity { get; set; }

    /// <summary>
    /// When <c>true</c> (default), <c>AddHttp()</c> registers
    /// <c>KaleidoStartupFilter</c> — an <c>IStartupFilter</c> that wires
    /// <c>ExceptionMiddleware</c> and <c>ObservabilityMiddleware</c> into the
    /// pipeline automatically. Set to <c>false</c> to suppress the filter and
    /// own the middleware order yourself; the host must then call the
    /// equivalent <c>UseMiddleware</c> registrations explicitly.
    /// </summary>
    public bool AutoRegisterMiddleware { get; set; } = true;

    /// <summary>
    /// Includes framework-generated diagnostics in Process execution responses when enabled.
    /// Off by default: <c>FrameworkMessages</c> stays empty, while handler-authored
    /// <c>BusinessMessages</c> is always returned.
    /// </summary>
    public bool IncludeFrameworkMessages { get; set; }
}

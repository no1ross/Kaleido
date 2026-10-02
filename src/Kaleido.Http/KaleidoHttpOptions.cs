using Microsoft.AspNetCore.Http;
namespace Kaleido.Http;

/// <summary>
/// Options for the Kaleido HTTP transport. Kaleido performs no authentication
/// itself — these options shape how declared capability authorization
/// (<c>[KaleidoAuthorization]</c>) and inbound correlation headers are
/// interpreted against the host's authentication middleware.
/// </summary>
public sealed class KaleidoHttpOptions
{
    /// <summary>
    /// When <c>true</c>, capabilities that do not declare
    /// <c>[KaleidoAuthorization]</c> require an authenticated caller
    /// (<c>User.Identity.IsAuthenticated</c>). When <c>false</c> (default),
    /// undeclared capabilities are open — matching
    /// <c>[Authorize]</c>-less endpoint behavior.
    /// </summary>
    public bool RequireAuthorization { get; set; }

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
    /// When <c>true</c>, creating a process requires an authenticated caller
    /// (401 when anonymous) — so every process is owned. Default
    /// <c>false</c>: anonymous creation is allowed and produces an unowned
    /// process.
    /// </summary>
    /// <remarks>
    /// Ownership enforcement itself is unconditional: once a process has an
    /// <c>Owner</c>, only the owner or a caller sharing an
    /// <c>OwnerRoles</c> entry may resume or read it.
    /// </remarks>
    public bool RequireProcessOwnership { get; set; }
}

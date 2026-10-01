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
}

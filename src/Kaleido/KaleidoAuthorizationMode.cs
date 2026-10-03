namespace Kaleido;

/// <summary>
/// How Kaleido enforces capability authorization (process steps, query
/// contexts, query views). Kaleido performs no authentication itself — it
/// evaluates the caller the transport derives from the host's authentication.
/// </summary>
public enum KaleidoAuthorizationMode
{
    /// <summary>
    /// Nothing is enforced: declared roles/policies, process ownership and
    /// registry filtering are all skipped. For samples, local development,
    /// and hosts without authentication. A startup warning is logged when
    /// capabilities declare authorization that this mode ignores.
    /// </summary>
    None = 0,

    /// <summary>
    /// Every capability requires an authenticated caller. A capability with
    /// no declaration is open to <b>any</b> authenticated caller;
    /// <c>[KaleidoAuthorization]</c> narrows access to roles/policy or opens
    /// it with <c>AllowAnonymous</c>.
    /// </summary>
    Authenticated = 1,

    /// <summary>
    /// Zero trust: nothing is reachable unless its declaration says who may
    /// reach it. Every capability must declare <c>Roles</c>, <c>Policy</c>,
    /// or <c>AllowAnonymous</c>. Undeclared capabilities fail the host at
    /// startup (<c>undeclared_authorization</c>) and are denied at runtime
    /// if one is ever evaluated.
    /// </summary>
    ZeroTrust = 2
}

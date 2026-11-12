namespace Kaleido.Server;

/// <summary>
/// How Kaleido enforces capability authorization (process steps, query
/// contexts, query views). Kaleido performs no authentication itself — it
/// evaluates the caller the transport derives from the host's authentication.
/// </summary>
public enum KaleidoAuthorizationMode
{
    /// <summary>
    /// The host does not enforce capability roles or policies. Intended for deliberately open
    /// local or test hosts; choose it explicitly (the default is <see cref="Authenticated"/>).
    /// Transport adapters may warn when a capability declares a rule this mode ignores.
    /// </summary>
    None = 0,

    /// <summary>
    /// The default when a host sets no mode (ADR 0021). Every capability requires an authenticated
    /// caller. A capability with
    /// no declaration is open to <b>any</b> authenticated caller;
    /// <c>[KaleidoAuthorization]</c> narrows access to roles/policy or opens
    /// it with <c>AllowAnonymous</c>.
    /// </summary>
    Authenticated = 1,

    /// <summary>
    /// Zero trust: nothing is reachable unless its declaration says who may
    /// reach it. Every capability must declare <c>Roles</c>, <c>Policy</c>,
    /// or <c>AllowAnonymous</c>, directly or through a service default.
    /// Capabilities with no applicable rule are omitted from discovery and
    /// endpoint mapping, and denied if submitted through a generic execute request.
    /// </summary>
    ZeroTrust = 2,
}

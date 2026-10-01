namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when a caller fails a capability's authorization requirement
/// (declared via <c>[KaleidoAuthorization]</c>) on a dynamic code path —
/// e.g. multi-step process execute, where per-step authorization is
/// evaluated inside the handler rather than by endpoint metadata.
/// Results in 401 when the caller is unauthenticated, 403 when
/// authenticated but denied.
/// </summary>
public sealed class KaleidoAuthorizationException(
    string capability,
    bool callerIsAuthenticated)
    : Exception($"Caller is not authorized for '{capability}'.")
{
    /// <summary>The capability name the caller was denied.</summary>
    public string Capability { get; } = capability;

    /// <summary>
    /// Whether the caller presented an authenticated identity — drives
    /// 401 vs 403 selection in the HTTP layer.
    /// </summary>
    public bool CallerIsAuthenticated { get; } = callerIsAuthenticated;
}

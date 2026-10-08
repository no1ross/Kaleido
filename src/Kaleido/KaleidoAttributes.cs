namespace Kaleido;

/// <summary>
/// Declares the authorization requirement for a Kaleido capability — a process step,
/// query context, or query view. Transport-agnostic metadata only: the HTTP layer
/// maps <see cref="Policy"/> to an ASP.NET named authorization policy and
/// <see cref="Roles"/> to role checks against the caller's claims principal.
/// Declarations take effect when <see cref="KaleidoServiceOptions.AuthorizationMode"/>
/// is <see cref="KaleidoAuthorizationMode.Authenticated"/> or
/// <see cref="KaleidoAuthorizationMode.ZeroTrust"/>: every capability then requires an
/// authenticated caller, and this attribute narrows access to roles/policy or opens it
/// with <see cref="AllowAnonymous"/>. In <c>ZeroTrust</c> every capability must declare
/// one of the three. In <c>None</c> nothing is enforced.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>Policy</c> — name of a policy the consumer defines via
/// <c>AddAuthorization(o => o.AddPolicy(...))</c>; evaluated by the transport.</item>
/// <item><c>Roles</c> — comma-delimited role names (same idiom as
/// <c>[Authorize(Roles = "...")]</c>); the caller must be in at least one.
/// App-role claims work for service-to-service callers.</item>
/// <item>Both declared → the caller must satisfy each.</item>
/// <item><c>AllowAnonymous</c> — the only way to open a capability to
/// unauthenticated callers; cannot be combined with <c>Roles</c>/<c>Policy</c>.</item>
/// </list>
/// Apply it to process steps, query sources and query views (never to query context
/// records). Query views without their own attribute inherit the authorization of
/// their query source.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class KaleidoAuthorizationAttribute : Attribute
{
    /// <summary>
    /// The name of a consumer-defined authorization policy evaluated by the
    /// transport layer (e.g. an ASP.NET named policy).
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// Comma-delimited role names (e.g. <c>"internal,admin"</c>); the caller
    /// must be in at least one. Kept as a string — matching
    /// <c>[Authorize(Roles = "...")]</c> — because attribute arguments only
    /// permit single-dimension arrays, and array literals here trip CA1861
    /// for consumers.
    /// </summary>
    public string? Roles { get; init; }

    /// <summary>
    /// Opens the capability to unauthenticated callers when authorization is
    /// enforced (e.g. adding an item to a cart before login). Combining it with
    /// <see cref="Roles"/> or <see cref="Policy"/> is a configuration error.
    /// </summary>
    public bool AllowAnonymous { get; init; }
}

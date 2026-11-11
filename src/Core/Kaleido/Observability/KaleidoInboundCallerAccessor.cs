using System.Security.Claims;

namespace Kaleido.Observability;

/// <summary>
/// The caller of the inbound request as the transport authenticated it: its principal and the
/// credential it presented (in HTTP, the <c>Authorization</c> value). Set by the transport once the
/// host's authentication ran; read by the outgoing Kaleido clients, so a call made for the caller is
/// authorized as that caller (ADR 0022). Holds a credential: never logged, never on events.
/// </summary>
internal interface IKaleidoInboundCallerAccessor
{
    /// <summary>True once the transport set the caller: the current scope handles an inbound request.</summary>
    bool IsInboundRequest { get; }

    /// <summary>The authenticated caller; <see langword="null"/> for an anonymous one.</summary>
    ClaimsPrincipal? Principal { get; }

    /// <summary>The credential the caller presented (e.g. <c>Bearer …</c>); <see langword="null"/> when none.</summary>
    string? Credential { get; }

    /// <summary>Sets the inbound caller for the current scope.</summary>
    /// <param name="principal">The request's principal; an unauthenticated one is recorded as anonymous.</param>
    /// <param name="credential">The credential the caller presented, if any.</param>
    void Set(ClaimsPrincipal? principal, string? credential);
}

internal sealed class KaleidoInboundCallerAccessor : IKaleidoInboundCallerAccessor
{
    public bool IsInboundRequest { get; private set; }

    public ClaimsPrincipal? Principal { get; private set; }

    public string? Credential { get; private set; }

    public void Set(ClaimsPrincipal? principal, string? credential)
    {
        IsInboundRequest = true;
        Principal = principal?.Identity?.IsAuthenticated == true ? principal : null;
        Credential = string.IsNullOrWhiteSpace(credential) ? null : credential;
    }
}

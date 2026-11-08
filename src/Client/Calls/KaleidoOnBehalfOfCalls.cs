namespace Kaleido.Client.Calls;

/// <summary>
/// How a service makes the calls it makes for a caller (<see cref="KaleidoCallPurpose.OnBehalfOfCaller"/>):
/// which token they carry. A setting, not a token: <c>KaleidoClientOptions.OnBehalfOfCalls</c> (Kaleido.Http.Client)
/// (<c>Kaleido:OnBehalfOfCalls</c>); the tokens themselves come from each request, or from the identity
/// provider through the host's handler, at the time of the call (#574).
/// </summary>
public enum KaleidoOnBehalfOfCalls
{
    /// <summary>
    /// The default: the caller's token, exactly as the inbound request carried it, whatever the host's
    /// handlers set. For services that all accept the same token (one platform audience).
    /// </summary>
    Forward = 0,

    /// <summary>
    /// The token the host's own handler set for the caller (<c>AddKaleidoClientHandler&lt;THandler&gt;()</c>),
    /// e.g. by an OAuth token exchange (RFC 8693) or On-Behalf-Of. Kaleido refuses the call when no token was
    /// set or the inbound one was sent unchanged (<c>HttpClientErrorCodes.CallerTokenNotExchanged</c>).
    /// Required on an MCP host: the MCP authorization specification forbids passing the agent's token through.
    /// </summary>
    Exchange = 1,
}

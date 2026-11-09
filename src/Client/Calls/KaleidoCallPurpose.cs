namespace Kaleido.Client.Calls;

/// <summary>Why a Kaleido client call is made; decided by Kaleido (<see cref="KaleidoCallContext.Purpose"/>).</summary>
public enum KaleidoCallPurpose
{
    /// <summary>
    /// For the caller of the inbound request being handled: an MCP tool call, a step handler's
    /// handoff or lookup, a delegated query. It carries the caller's token (<see cref="KaleidoOnBehalfOfCalls.Forward"/>)
    /// or the one the host's handler set for that caller (<see cref="KaleidoOnBehalfOfCalls.Exchange"/>) - never the
    /// service's own credential.
    /// </summary>
    OnBehalfOfCaller = 0,

    /// <summary>
    /// The service's own call: a registry fetch or refresh (shared by every caller), a health probe,
    /// or any call made outside an inbound request. It carries what the host's handlers set (e.g. a
    /// client-credentials token), never a caller's.
    /// </summary>
    ServiceOwn = 1,
}

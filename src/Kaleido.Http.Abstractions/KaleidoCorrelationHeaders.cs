namespace Kaleido.Http;

/// <summary>
/// HTTP header names used to propagate Kaleido correlation context between services.
/// Request id and process id are end-to-end (forwarded unchanged on every hop); calling
/// processor and calling step are per-hop (set by the caller, never forwarded).
/// </summary>
public static class KaleidoCorrelationHeaders
{
    /// <summary>End-to-end: the unique identifier of the originating request.</summary>
    public const string RequestId =
        "X-Kaleido-Request-Id";

    /// <summary>End-to-end: the process instance the request belongs to.</summary>
    public const string ProcessId =
        "X-Kaleido-Process-Id";

    /// <summary>
    /// Per-hop: the processor whose step is making this call. Sent only when the call is
    /// made from inside a step execution; never forwarded to the next hop.
    /// </summary>
    public const string CallingProcessor =
        "X-Kaleido-Calling-Processor";

    /// <summary>Per-hop: the step in the calling processor that is making this call.</summary>
    public const string CallingStep =
        "X-Kaleido-Calling-Step";
}

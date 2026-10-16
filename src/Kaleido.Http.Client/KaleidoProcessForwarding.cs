namespace Kaleido.Http.Client;

/// <summary>
/// Makes Kaleido client calls carry a process id the inbound request did not send as a header. A
/// front door whose callers cannot set HTTP headers per call — an AI-tool (MCP) adapter, where the
/// process id arrives as a tool argument — opens a scope with that id, and every Kaleido client
/// call inside it sends <c>X-Kaleido-Process-Id</c>, so the called service records the call with
/// the process (traces, logs, query events).
/// </summary>
/// <remarks>
/// <para>Opt-in per call: only calls made inside <see cref="ForwardProcess"/> are affected. Outside
/// a scope the process id comes from the request's correlation context, as before.</para>
/// <para>Precedence on an outbound call: an explicit process id passed to a client method (e.g.
/// <c>ExecuteStepAsync(stepName, step, processId)</c>) wins, then this scope, then the correlation
/// context. The scope is never filled from inbound headers; it is the front door's statement of
/// which process the call belongs to.</para>
/// <para>The same pattern as <see cref="IKaleidoCallerForwarding"/>: the value flows with the async
/// call and does not leak into other requests.</para>
/// </remarks>
internal interface IKaleidoProcessForwarding
{
    /// <summary>The process id of the current scope (on this async flow); null outside a scope or for a scope without one.</summary>
    Guid? ProcessId { get; }

    /// <summary>
    /// Starts a scope in which Kaleido client calls send <paramref name="processId"/> as
    /// <c>X-Kaleido-Process-Id</c>. Pass null to send none from this scope (the correlation context's
    /// id, if any, still applies). Dispose the scope to end it; nested scopes restore the outer one.
    /// </summary>
    /// <param name="processId">The process the calls in this scope belong to.</param>
    /// <returns>The scope; dispose it to restore the previous value.</returns>
    IDisposable ForwardProcess(Guid? processId);
}

internal sealed class KaleidoProcessForwarding : IKaleidoProcessForwarding
{
    private static readonly AsyncLocal<ProcessScope?> Current = new();

    public Guid? ProcessId => Current.Value?.ProcessId;

    public IDisposable ForwardProcess(Guid? processId)
    {
        var previous = Current.Value;

        Current.Value = new ProcessScope(processId);

        return new Restore(previous);
    }

    private sealed record ProcessScope(Guid? ProcessId);

    private sealed class Restore(ProcessScope? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}

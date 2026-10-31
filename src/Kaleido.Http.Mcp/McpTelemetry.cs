namespace Kaleido.Http.Mcp;

/// <summary>
/// Activity source, meter, instrument and tag names of the Kaleido MCP adapter, part of its stable
/// telemetry contract. Register them with <c>AddKaleidoMcpInstrumentation()</c> (included in
/// <c>AddKaleidoInstrumentation()</c>) from <c>Kaleido.Observability.OpenTelemetry</c>.
/// </summary>
internal static class McpTelemetry
{
    // ── ActivitySource / Meter ───────────────────────────────────────────────

    public const string ActivitySourceName = "Kaleido.Http.Mcp";

    /// <summary>The origin (<c>X-Kaleido-Origin</c>) the adapter stamps on its tool calls (ADR 0022).</summary>
    public const string Origin = "mcp";

    public const string MeterName = "Kaleido.Http.Mcp";

    // ── Activity names ───────────────────────────────────────────────────────

    /// <summary>One MCP <c>tools/call</c>; Kaleido HTTP calls it makes are its children.</summary>
    public const string ToolCallActivityName = "kaleido.mcp.tool.call";

    /// <summary>One MCP <c>tools/list</c>.</summary>
    public const string ToolsListActivityName = "kaleido.mcp.tools.list";

    // ── Metric names ─────────────────────────────────────────────────────────

    /// <summary>Tool calls, by kind, service and outcome (and error code when it failed).</summary>
    public const string ToolCallsCounterName = "kaleido.mcp.tool_calls";

    /// <summary>Tool call duration in seconds, by kind, service and outcome.</summary>
    public const string ToolCallDurationHistogramName = "kaleido.mcp.tool_call.duration";

    /// <summary>Registry reads, by result: <c>hit</c> (the caller's registry revision was already translated into tools) or <c>fetched</c> (translated now).</summary>
    public const string RegistryReadsCounterName = "kaleido.mcp.registry_reads";

    /// <summary>
    /// States handed to the MCP client and refused when they came back (#452, #516), by kind
    /// (<c>lookup</c>, <c>continuation</c>, <c>answers</c>, <c>confirmation</c>) and reason:
    /// <c>unknown_key</c> (the instances do not share a Data Protection key ring), <c>expired</c>,
    /// <c>another_caller</c> or <c>invalid</c>.
    /// </summary>
    public const string StatesRefusedCounterName = "kaleido.mcp.states_refused";

    // ── Tag names ────────────────────────────────────────────────────────────

    public const string TagTool = "kaleido.mcp.tool";

    /// <summary><c>query</c>, <c>step</c>, <c>answer</c>, <c>state</c>, or <c>unknown</c>.</summary>
    public const string TagToolKind = "kaleido.mcp.tool.kind";

    public const string TagService = "kaleido.mcp.service";

    public const string TagCapability = "kaleido.mcp.capability";

    /// <summary><c>completed</c>, <c>input_required</c>, <c>error</c>, or <c>canceled</c>.</summary>
    public const string TagOutcome = "kaleido.mcp.outcome";

    /// <summary>The <c>mcp_*</c> code of a failed call (same key as the HTTP error tag).</summary>
    public const string TagErrorCode = "kaleido.error.code";

    public const string TagToolCount = "kaleido.mcp.tool_count";

    public const string TagRegistryResult = "kaleido.mcp.registry.result";

    public const string TagRegistryRevision = "kaleido.mcp.registry.revision";

    public const string TagRegistryPartial = "kaleido.mcp.registry.partial";

    /// <summary>Why a state was refused; see <see cref="StatesRefusedCounterName"/>.</summary>
    public const string TagStateRefusal = "kaleido.mcp.state.refusal";

    /// <summary>The kind of a refused state; see <see cref="StatesRefusedCounterName"/>.</summary>
    public const string TagStateKind = "kaleido.mcp.state.kind";

    // ── Tag values ───────────────────────────────────────────────────────────

    public const string OutcomeCompleted = "completed";

    public const string OutcomeInputRequired = "input_required";

    public const string OutcomeError = "error";

    public const string OutcomeCanceled = "canceled";

    public const string RegistryHit = "hit";

    public const string RegistryFetched = "fetched";

    public const string StateUnknownKey = "unknown_key";

    public const string StateExpired = "expired";

    public const string StateAnotherCaller = "another_caller";

    public const string StateInvalid = "invalid";

    public const string StateKindLookup = "lookup";

    public const string StateKindContinuation = "continuation";

    public const string StateKindAnswers = "answers";

    public const string StateKindConfirmation = "confirmation";
}

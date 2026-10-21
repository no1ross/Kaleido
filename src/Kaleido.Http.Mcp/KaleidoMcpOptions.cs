namespace Kaleido.Http.Mcp;

/// <summary>
/// Immutable settings of the Kaleido MCP adapter, built by <see cref="KaleidoMcpOptionsBuilder"/>
/// from the <c>Kaleido:Mcp</c> configuration section and the <c>AddMcp</c> configure delegate.
/// </summary>
/// <remarks>
/// The adapter is a layer on top of the Kaleido HTTP stack: the caller's tools come from the host's
/// registry (<c>IKaleidoRegistryService</c>, filtered per caller), and tool calls go through the
/// Kaleido clients (<c>AddHttpClients()</c>) with the caller's own token. It runs in any Kaleido host
/// that aggregates the services it publishes — a router, or a dedicated MCP host.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record KaleidoMcpOptions
{
    /// <summary>The configuration section bound by <c>AddMcp</c>.</summary>
    public const string SectionName = "Kaleido:Mcp";

    /// <summary>The MCP server name reported to clients.</summary>
    public string ServerName { get; init; } = "kaleido";

    /// <summary>The MCP server version reported to clients.</summary>
    public string ServerVersion { get; init; } = "1.0.0";

    /// <summary>
    /// The instructions sent to the agent when it connects: how Kaleido tools fit together (the
    /// processId thread, verbatim messages, user-only answers). Resolved from
    /// <see cref="KaleidoMcpOptionsBuilder.Instructions"/>,
    /// <see cref="KaleidoMcpOptionsBuilder.InstructionsFile"/> or the packaged defaults, plus
    /// <see cref="KaleidoMcpOptionsBuilder.AdditionalInstructions"/>.
    /// </summary>
    public required string Instructions { get; init; }

    /// <summary>
    /// The maximum number of information-request rounds put to the user in one tool call before
    /// the remaining questions are returned to the agent.
    /// </summary>
    /// <remarks>
    /// Zero or more; the default is 5. Zero never puts questions to the user automatically: every
    /// information request is returned to the agent. A negative value fails <c>AddMcp</c> with
    /// <see cref="McpErrorCodes.InvalidOptions"/>.
    /// </remarks>
    public int MaxElicitationRounds { get; init; } = 5;

    /// <summary>Which tools <c>tools/list</c> offers; see <see cref="McpToolListing"/>.</summary>
    /// <remarks>The default, <see cref="McpToolListing.All"/>, lists every tool the caller may use.</remarks>
    public McpToolListing ToolListing { get; init; } = McpToolListing.All;

    /// <summary>
    /// With <see cref="McpToolListing.FrontDoor"/>: more tools to list by name (for example a search
    /// no starting step declares as a lookup, such as "find a previous request"). Names that are not
    /// tools of the host are reported at Warning when the tools are built. Ignored with
    /// <see cref="McpToolListing.All"/>.
    /// </summary>
    public IReadOnlyList<string> ListedTools { get; init; } = [];
}

/// <summary>Which of the caller's tools the MCP adapter lists in <c>tools/list</c>.</summary>
/// <remarks>
/// Listing decides only what the agent sees up front. Every tool of the caller stays callable, with
/// the same authorization and process rules, whichever mode is used.
/// </remarks>
public enum McpToolListing
{
    /// <summary>
    /// Every query and step tool the caller may use, plus the built-ins (today's behavior). Best for
    /// small hosts: every tool has its own typed definition and its own approval prompt in clients.
    /// </summary>
    All,

    /// <summary>
    /// A small, fixed menu for hosts with many capabilities: the steps that can start a process,
    /// the queries those steps declare as lookups, the configured
    /// <see cref="KaleidoMcpOptions.ListedTools"/>, and the built-ins, including
    /// <c>search_tools</c>, <c>describe_tool</c> and <c>call_tool</c>. Steps that are open in a
    /// process are described in each step result's <c>next</c> (inputs, approval hints, the lookups
    /// they need with the processId filled in) and run with <c>call_tool</c>. The menu does not grow
    /// with the number of non-initial steps or unlisted queries.
    /// </summary>
    FrontDoor,
}

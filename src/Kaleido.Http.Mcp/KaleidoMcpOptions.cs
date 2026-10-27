namespace Kaleido.Http.Mcp;

/// <summary>
/// Immutable settings of the Kaleido MCP adapter: <c>AddMcp</c> lays the <c>Kaleido:Mcp</c> configuration
/// section over a copy of the instance passed in code (configuration overrides code, setting by
/// setting), validates it and registers it (ADR 0030).
/// </summary>
/// <remarks>
/// The adapter is a layer on top of the Kaleido HTTP stack: the caller's tools come from the host's
/// registry (<c>IKaleidoRegistryService</c>, filtered per caller), and tool calls go through the
/// Kaleido clients (<c>AddHttpClients()</c>) with the caller's own token. It runs in any Kaleido host
/// that aggregates the services it publishes — a router, or a dedicated MCP host.
/// </remarks>
public sealed record KaleidoMcpOptions
{
    /// <summary>The configuration section that overrides these options: <c>Kaleido:Mcp</c>.</summary>
    public const string SectionName = "Kaleido:Mcp";

    /// <summary>The MCP server name reported to clients. Default <c>kaleido</c>; must not be blank.</summary>
    public string ServerName { get; init; } = "kaleido";

    /// <summary>The MCP server version reported to clients. Must not be blank.</summary>
    public string ServerVersion { get; init; } = "1.0.0";

    /// <summary>
    /// Replaces the packaged default instructions - what the agent is told when it connects (how
    /// Kaleido tools fit together: the processId thread, verbatim messages, user-only answers) - with
    /// this text. <see langword="null"/> (the default) keeps the packaged instructions; use
    /// <see cref="AdditionalInstructions"/> to extend them. Must not be blank, and not set together
    /// with <see cref="InstructionsFile"/>.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Replaces the packaged default instructions with the content of this file (e.g. a Markdown
    /// prompt maintained with the domain). Relative paths resolve against the application base
    /// directory; a missing file fails <c>AddMcp</c> (<see cref="McpErrorCodes.MissingInstructionsFile"/>).
    /// Must not be blank, and not set together with <see cref="Instructions"/>.
    /// </summary>
    public string? InstructionsFile { get; init; }

    /// <summary>
    /// Appended to the instructions: domain guidance for the agent (e.g. how a business process
    /// usually starts), without repeating the generic rules. Must not be blank when set.
    /// </summary>
    public string? AdditionalInstructions { get; init; }

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
    /// <see cref="McpToolListing.All"/>. A blank or repeated name fails <c>AddMcp</c>.
    /// </summary>
    public IReadOnlyList<string> ListedTools { get; init; } = [];

    /// <summary>
    /// Asks the user, in a form the MCP client shows, before running a step declared
    /// <c>[ProcessStep(RequiresConfirmation = true)]</c>, and runs it only when the user accepts
    /// (#439). Off by default: the declaration is then a hint in the tool description.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The form names the step and its inputs. Nothing runs when the user declines, cancels, or
    /// leaves the confirmation unchecked; the agent's own statement that the user agreed, or a
    /// confirmation argument it adds, is never accepted. A client that cannot show forms gets
    /// <see cref="McpErrorCodes.ConfirmationRequired"/> and the step is not run.
    /// </para>
    /// <para>
    /// This raises the bar; it is not proof of consent. The MCP client relays the form and its answer,
    /// and Kaleido cannot tell a person's answer from a client that answers forms itself. A step whose
    /// consent must be proven checks it in the service (for example a signed approval in its input).
    /// </para>
    /// </remarks>
    public bool RequireConfirmation { get; init; }

    // Run by AddMcp after configuration is laid over the options. Nothing is trimmed or replaced: a
    // blank, repeated, out-of-range or conflicting value stops startup.
    internal static void Validate(KaleidoMcpOptions options)
    {
        Require(!string.IsNullOrWhiteSpace(options.ServerName), nameof(ServerName), "must not be blank");
        Require(!string.IsNullOrWhiteSpace(options.ServerVersion), nameof(ServerVersion), "must not be blank");
        Require(options.Instructions is null || !string.IsNullOrWhiteSpace(options.Instructions), nameof(Instructions), "must not be blank when set");
        Require(options.InstructionsFile is null || !string.IsNullOrWhiteSpace(options.InstructionsFile), nameof(InstructionsFile), "must not be blank when set");
        Require(options.AdditionalInstructions is null || !string.IsNullOrWhiteSpace(options.AdditionalInstructions), nameof(AdditionalInstructions), "must not be blank when set");
        Require(options.Instructions is null || options.InstructionsFile is null, nameof(InstructionsFile), "must not be set together with Instructions (each replaces the packaged instructions)");
        Require(options.MaxElicitationRounds >= 0, nameof(MaxElicitationRounds), $"is {options.MaxElicitationRounds.ToString(System.Globalization.CultureInfo.InvariantCulture)}; it must be zero or more (0 returns every information request to the agent)");
        Require(Enum.IsDefined(options.ToolListing), nameof(ToolListing), $"is '{options.ToolListing}'; use {nameof(McpToolListing.All)} or {nameof(McpToolListing.FrontDoor)}");
        Require(options.ListedTools.All(x => !string.IsNullOrWhiteSpace(x)), nameof(ListedTools), "must not contain a blank name");
        Require(new HashSet<string>(options.ListedTools, StringComparer.Ordinal).Count == options.ListedTools.Count, nameof(ListedTools), "must not repeat a name");
    }

    private static void Require(bool condition, string name, string rule)
    {
        if (!condition)
        {
            throw new KaleidoConfigurationException(McpErrorCodes.InvalidOptions, $"{SectionName}:{name} {rule}.");
        }
    }
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

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
    public int MaxElicitationRounds { get; init; } = 5;
}

/// <summary>
/// Mutable bind/configure target for <see cref="KaleidoMcpOptions"/>: configuration binding needs
/// settable properties; <c>AddMcp</c> binds it, applies the configure delegate, validates, and
/// registers the immutable snapshot.
/// </summary>
public sealed class KaleidoMcpOptionsBuilder
{
    private const string DefaultInstructionsResource = "Kaleido.Http.Mcp.Instructions.DefaultInstructions.md";

    /// <inheritdoc cref="KaleidoMcpOptions.ServerName"/>
    public string ServerName { get; set; } = "kaleido";

    /// <inheritdoc cref="KaleidoMcpOptions.ServerVersion"/>
    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Replaces the packaged default instructions with this text. Leave empty to keep the defaults
    /// (and use <see cref="AdditionalInstructions"/> to extend them).
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Replaces the packaged default instructions with the content of this file (e.g. a Markdown
    /// prompt maintained with the domain). Relative paths resolve against the application base
    /// directory. Ignored when <see cref="Instructions"/> is set.
    /// </summary>
    public string? InstructionsFile { get; set; }

    /// <summary>
    /// Appended to the instructions: domain guidance for the agent (e.g. how a business process
    /// usually starts), without repeating the generic rules.
    /// </summary>
    public string? AdditionalInstructions { get; set; }

    /// <inheritdoc cref="KaleidoMcpOptions.MaxElicitationRounds"/>
    public int MaxElicitationRounds { get; set; } = 5;

    internal KaleidoMcpOptions Build() =>
        new()
        {
            ServerName = string.IsNullOrWhiteSpace(ServerName) ? "kaleido" : ServerName.Trim(),
            ServerVersion = string.IsNullOrWhiteSpace(ServerVersion) ? "1.0.0" : ServerVersion.Trim(),
            Instructions = ResolveInstructions(),
            MaxElicitationRounds = Math.Max(0, MaxElicitationRounds),
        };

    private string ResolveInstructions()
    {
        var instructions =
            !string.IsNullOrWhiteSpace(Instructions)
                ? Instructions.Trim()
                : !string.IsNullOrWhiteSpace(InstructionsFile)
                    ? ReadInstructionsFile(InstructionsFile)
                    : ReadDefaultInstructions();

        return string.IsNullOrWhiteSpace(AdditionalInstructions)
            ? instructions
            : instructions + "\n\n" + AdditionalInstructions.Trim();
    }

    private static string ReadInstructionsFile(string path)
    {
        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Join(AppContext.BaseDirectory, path);

        return File.Exists(fullPath)
            ? File.ReadAllText(fullPath).Trim()
            : throw new KaleidoConfigurationException(
                McpErrorCodes.MissingInstructionsFile,
                $"Kaleido MCP InstructionsFile '{fullPath}' ({KaleidoMcpOptions.SectionName}:InstructionsFile) was not found.");
    }

    private static string ReadDefaultInstructions()
    {
        using var stream =
            typeof(KaleidoMcpOptionsBuilder).Assembly.GetManifestResourceStream(DefaultInstructionsResource)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"The packaged MCP instructions resource '{DefaultInstructionsResource}' is missing.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().Trim();
    }
}

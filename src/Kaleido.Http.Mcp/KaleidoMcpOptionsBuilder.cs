using System.Globalization;

namespace Kaleido.Http.Mcp;

/// <summary>
/// Mutable bind/configure target for <see cref="KaleidoMcpOptions"/>: configuration binding needs
/// settable properties; <c>AddMcp</c> binds it, applies the configure delegate, validates, and
/// registers the immutable snapshot.
/// </summary>
public sealed class KaleidoMcpOptionsBuilder
{
    private const string DefaultInstructionsResource = "Kaleido.Http.Mcp.Instructions.DefaultInstructions.md";
    private const string FrontDoorInstructionsResource = "Kaleido.Http.Mcp.Instructions.FrontDoorInstructions.md";

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

    /// <inheritdoc cref="KaleidoMcpOptions.ToolListing"/>
    public McpToolListing ToolListing { get; set; } = McpToolListing.All;

    /// <inheritdoc cref="KaleidoMcpOptions.ListedTools"/>
    public IReadOnlyList<string> ListedTools { get; set; } = [];

    internal KaleidoMcpOptions Build()
    {
        if (MaxElicitationRounds < 0)
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.InvalidOptions,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{KaleidoMcpOptions.SectionName}:{nameof(MaxElicitationRounds)} is {MaxElicitationRounds}; it must be zero or more (0 returns every information request to the agent)."));
        }

        if (!Enum.IsDefined(ToolListing))
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.InvalidOptions,
                $"{KaleidoMcpOptions.SectionName}:{nameof(ToolListing)} is '{ToolListing}'; use {nameof(McpToolListing.All)} or {nameof(McpToolListing.FrontDoor)}.");
        }

        return new()
        {
            ServerName = string.IsNullOrWhiteSpace(ServerName) ? "kaleido" : ServerName.Trim(),
            ServerVersion = string.IsNullOrWhiteSpace(ServerVersion) ? "1.0.0" : ServerVersion.Trim(),
            Instructions = ResolveInstructions(),
            MaxElicitationRounds = MaxElicitationRounds,
            ToolListing = ToolListing,
            ListedTools = ListedTools
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private string ResolveInstructions()
    {
        var instructions =
            !string.IsNullOrWhiteSpace(Instructions)
                ? Instructions.Trim()
                : !string.IsNullOrWhiteSpace(InstructionsFile)
                    ? ReadInstructionsFile(InstructionsFile)
                    : ReadResource(DefaultInstructionsResource);

        // The front door changes how unlisted tools are reached; the agent is told whatever the base text.
        if (ToolListing == McpToolListing.FrontDoor)
        {
            instructions += "\n\n" + ReadResource(FrontDoorInstructionsResource);
        }

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

    private static string ReadResource(string name)
    {
        using var stream =
            typeof(KaleidoMcpOptionsBuilder).Assembly.GetManifestResourceStream(name)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"The packaged MCP instructions resource '{name}' is missing.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().Trim();
    }
}

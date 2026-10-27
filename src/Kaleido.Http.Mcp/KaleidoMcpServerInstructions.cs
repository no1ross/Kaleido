namespace Kaleido.Http.Mcp;

/// <summary>
/// The instructions the MCP server sends an agent when it connects, resolved once by <c>AddMcp</c>
/// from <see cref="KaleidoMcpOptions"/> (ADR 0030: the options keep what the host supplied; what is
/// derived from them lives here).
/// </summary>
/// <param name="Text">The full instructions text.</param>
internal sealed record KaleidoMcpServerInstructions(string Text)
{
    private const string DefaultInstructionsResource = "Kaleido.Http.Mcp.Instructions.DefaultInstructions.md";
    private const string FrontDoorInstructionsResource = "Kaleido.Http.Mcp.Instructions.FrontDoorInstructions.md";

    /// <summary>
    /// <see cref="KaleidoMcpOptions.Instructions"/>, else the content of
    /// <see cref="KaleidoMcpOptions.InstructionsFile"/>, else the packaged defaults; then the front-door
    /// guidance when <see cref="McpToolListing.FrontDoor"/> is used, then
    /// <see cref="KaleidoMcpOptions.AdditionalInstructions"/>, separated by blank lines.
    /// </summary>
    /// <param name="options">Validated options.</param>
    /// <returns>The resolved instructions.</returns>
    /// <exception cref="KaleidoConfigurationException">The instructions file does not exist (<see cref="McpErrorCodes.MissingInstructionsFile"/>).</exception>
    public static KaleidoMcpServerInstructions Resolve(KaleidoMcpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var parts = new List<string>
        {
            options.Instructions
                ?? (options.InstructionsFile is { } file ? ReadInstructionsFile(file) : ReadResource(DefaultInstructionsResource)),
        };

        // The front door changes how unlisted tools are reached; the agent is told whatever the base text.
        if (options.ToolListing == McpToolListing.FrontDoor)
        {
            parts.Add(ReadResource(FrontDoorInstructionsResource));
        }

        if (options.AdditionalInstructions is { } additional)
        {
            parts.Add(additional);
        }

        return new(string.Join("\n\n", parts));
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
            typeof(KaleidoMcpServerInstructions).Assembly.GetManifestResourceStream(name)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"The packaged MCP instructions resource '{name}' is missing.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().Trim();
    }
}

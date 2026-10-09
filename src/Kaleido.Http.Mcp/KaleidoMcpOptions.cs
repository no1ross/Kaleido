namespace Kaleido.Http.Mcp;

/// <summary>
/// Immutable settings of the Kaleido MCP adapter, built by <see cref="KaleidoMcpOptionsBuilder"/>
/// from the <c>Kaleido:Mcp</c> configuration section and the <c>AddKaleidoMcp</c> configure delegate.
/// </summary>
/// <remarks>
/// The adapter is a pure HTTP consumer of Kaleido: it reads the registry at
/// <see cref="BaseUrl"/> + <see cref="RegistryPath"/> and calls the capability URLs it lists,
/// forwarding the MCP caller's <c>Authorization</c> header. It can therefore run inside a router
/// (base URL = the router itself) or as its own deployable pointed at a router or a service.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record KaleidoMcpOptions
{
    /// <summary>The configuration section bound by <c>AddKaleidoMcp</c>.</summary>
    public const string SectionName = "Kaleido:Mcp";

    /// <summary>The Kaleido HTTP endpoint the adapter talks to (a router or a single service).</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>
    /// The registry path on <see cref="BaseUrl"/>, e.g. <c>/router/registry</c> for an aggregating
    /// router or <c>/intake/registry</c> for one service. The registry is filtered per caller, so
    /// each agent sees only the tools its token may use.
    /// </summary>
    public required string RegistryPath { get; init; }

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
    /// How long a caller's tool list is reused before the registry is read again. Tools change
    /// when capabilities are deployed or a caller's access changes; keep this short.
    /// </summary>
    public TimeSpan RegistryCacheDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The maximum number of information-request rounds answered through elicitation in one tool
    /// call before the remaining questions are returned to the agent.
    /// </summary>
    public int MaxElicitationRounds { get; init; } = 5;
}

/// <summary>
/// Mutable bind/configure target for <see cref="KaleidoMcpOptions"/>: configuration binding needs
/// settable properties; <c>AddKaleidoMcp</c> binds it, applies the configure delegate, validates,
/// and registers the immutable snapshot.
/// </summary>
public sealed class KaleidoMcpOptionsBuilder
{
    private const string DefaultInstructionsResource = "Kaleido.Http.Mcp.Instructions.DefaultInstructions.md";

    /// <inheritdoc cref="KaleidoMcpOptions.BaseUrl"/>
    public string? BaseUrl { get; set; }

    /// <inheritdoc cref="KaleidoMcpOptions.RegistryPath"/>
    public string? RegistryPath { get; set; }

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

    /// <inheritdoc cref="KaleidoMcpOptions.RegistryCacheDuration"/>
    public TimeSpan RegistryCacheDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc cref="KaleidoMcpOptions.MaxElicitationRounds"/>
    public int MaxElicitationRounds { get; set; } = 5;

    internal KaleidoMcpOptions Build()
    {
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUrl) ||
            !(string.Equals(baseUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
              string.Equals(baseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.MissingBaseUrl,
                $"Kaleido MCP needs an absolute http(s) BaseUrl ({KaleidoMcpOptions.SectionName}:BaseUrl), e.g. http://localhost:8080.");
        }

        if (string.IsNullOrWhiteSpace(RegistryPath))
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.MissingRegistryPath,
                $"Kaleido MCP needs a RegistryPath ({KaleidoMcpOptions.SectionName}:RegistryPath), e.g. /router/registry.");
        }

        return new KaleidoMcpOptions
        {
            BaseUrl = baseUrl,
            RegistryPath = "/" + RegistryPath.Trim().TrimStart('/'),
            ServerName = string.IsNullOrWhiteSpace(ServerName) ? "kaleido" : ServerName.Trim(),
            ServerVersion = string.IsNullOrWhiteSpace(ServerVersion) ? "1.0.0" : ServerVersion.Trim(),
            Instructions = ResolveInstructions(),
            RegistryCacheDuration = RegistryCacheDuration < TimeSpan.Zero ? TimeSpan.Zero : RegistryCacheDuration,
            MaxElicitationRounds = Math.Max(0, MaxElicitationRounds)
        };
    }

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
            : Path.Combine(AppContext.BaseDirectory, path);

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

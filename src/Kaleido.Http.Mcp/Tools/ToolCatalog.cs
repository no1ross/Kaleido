using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>What a tool does when called.</summary>
internal enum KaleidoToolKind
{
    /// <summary>A read-only query of a source or view (<c>POST queryUrl</c>).</summary>
    Query,

    /// <summary>One process step (<c>POST executeUrl</c>, the process id in a header).</summary>
    Step,

    /// <summary>Submits the user's answers to a pending information request.</summary>
    AnswerInformationRequest,

    /// <summary>Reads a process's state, next steps and pending questions.</summary>
    GetProcessState
}

/// <summary>An MCP tool and what it maps to in Kaleido.</summary>
[ExcludeFromCodeCoverage]
internal sealed record KaleidoTool
{
    /// <summary>The MCP tool definition the agent sees.</summary>
    public required Tool Definition { get; init; }

    public required KaleidoToolKind Kind { get; init; }

    /// <summary>The Kaleido service that owns the capability.</summary>
    public string Service { get; init; } = string.Empty;

    /// <summary>The capability name in the registry (source, view or step type name).</summary>
    public string CapabilityName { get; init; } = string.Empty;

    /// <summary>The Kaleido URL the tool calls (<c>queryUrl</c> or <c>executeUrl</c>).</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>For a step: it may start a new process (no processId needed).</summary>
    public bool IsInitial { get; init; }
}

/// <summary>
/// One caller's tools, translated from the caller-filtered registry. A view of the registry, not
/// a second registry: it holds nothing the registry doesn't publish, and is rebuilt when the
/// registry revision changes.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ToolCatalog
{
    public static ToolCatalog Empty { get; } = new();

    public IReadOnlyList<KaleidoTool> Tools { get; init; } = [];

    /// <summary>The registry revision the catalog was built from.</summary>
    public string? Revision { get; init; }

    /// <summary>The registry was partial (a downstream service failed): some tools are missing.</summary>
    public bool IsPartial { get; init; }

    /// <summary>Services whose registry could not be read when the registry was partial.</summary>
    public IReadOnlyList<string> UnavailableServices { get; init; } = [];

    /// <summary>Services with processes (the values of the built-in tools' <c>service</c> argument).</summary>
    public IReadOnlyList<string> ProcessServices { get; init; } = [];

    /// <summary>Capabilities and inputs without a description ("tool" or "tool.argument").</summary>
    public IReadOnlyList<string> MissingDescriptions { get; init; } = [];

    /// <summary>Tool names that were shortened to fit MCP's 64-character limit.</summary>
    public IReadOnlyList<string> TruncatedNames { get; init; } = [];

    private IReadOnlyDictionary<string, KaleidoTool>? byName;
    private IReadOnlyDictionary<string, KaleidoTool>? byUrl;

    public KaleidoTool? Find(string toolName)
    {
        byName ??= Tools.ToDictionary(x => x.Definition.Name, StringComparer.Ordinal);

        return byName.TryGetValue(toolName, out var tool) ? tool : null;
    }

    /// <summary>The tool that calls <paramref name="url"/> (maps next steps back to tool names).</summary>
    public KaleidoTool? FindByUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        byUrl ??= Tools
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        return byUrl.TryGetValue(url, out var tool) ? tool : null;
    }
}

using System.Collections.Frozen;
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
    GetProcessState,
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

    /// <summary>For a query tool: the source's published name.</summary>
    public string? SourceName { get; init; }

    /// <summary>For a view's query tool: the view's published name (null for a direct source query).</summary>
    public string? ViewName { get; init; }

    /// <summary>
    /// The capability's URL as published (<c>queryUrl</c> or <c>executeUrl</c>); used to map a
    /// process's next steps back to tool names. Calls go through the Kaleido clients by name.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>For a step: it may start a new process (no processId needed).</summary>
    public bool IsInitial { get; init; }

    /// <summary>For a step: its inputs that take their values from one result of a query.</summary>
    public IReadOnlyList<ToolLookup> Lookups { get; init; } = [];

    /// <summary>For a step: the steps whose completion closes it (its published <c>availableUntil</c>).</summary>
    public IReadOnlyList<string> ClosedBy { get; init; } = [];
}

/// <summary>
/// A step's lookup, resolved against the caller's tools: the step inputs filled from one result of
/// a query, and the query tool that finds it (null when the caller has no such tool).
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ToolLookup
{
    public required string Group { get; init; }

    public required string Service { get; init; }

    public required string Source { get; init; }

    public string? View { get; init; }

    /// <summary>The query tool that finds the result; null when the caller may not use it or it is not published.</summary>
    public string? QueryTool { get; init; }

    /// <summary>The query tool's title (the source's or view's display name).</summary>
    public string? QueryTitle { get; init; }

    /// <summary>Step argument -> result field, for every input of the group.</summary>
    public required IReadOnlyList<(string Argument, string Field)> Inputs { get; init; }

    /// <summary>The result fields that identify a result to a person.</summary>
    public IReadOnlyList<string> Display { get; init; } = [];
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

    /// <summary>
    /// Why there are no tools when the registry could not be read (and no earlier tools could be
    /// reused); null when the catalog is current.
    /// </summary>
    public string? Unavailable { get; init; }

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

    /// <summary>Step inputs whose lookup names no query tool of this caller ("tool.argument -> service/source[/view]").</summary>
    public IReadOnlyList<string> UnresolvedLookups { get; init; } = [];

    /// <summary>Capabilities whose derived tool names collided, and the names they got instead.</summary>
    public IReadOnlyList<string> NameCollisions { get; init; } = [];

    private FrozenDictionary<string, KaleidoTool>? byName;
    private FrozenDictionary<string, KaleidoTool>? byUrl;

    public ToolCatalog()
    {
    }

    // A 'with' copy may change the tools: it must not keep the original's lookups.
    private ToolCatalog(ToolCatalog original)
    {
        Tools = original.Tools;
        Revision = original.Revision;
        Unavailable = original.Unavailable;
        IsPartial = original.IsPartial;
        UnavailableServices = original.UnavailableServices;
        ProcessServices = original.ProcessServices;
        MissingDescriptions = original.MissingDescriptions;
        TruncatedNames = original.TruncatedNames;
        UnresolvedLookups = original.UnresolvedLookups;
        NameCollisions = original.NameCollisions;
    }

    public KaleidoTool? Find(string toolName)
    {
        byName ??= Tools.ToFrozenDictionary(x => x.Definition.Name, StringComparer.Ordinal);

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
            .ToFrozenDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        return byUrl.TryGetValue(url, out var tool) ? tool : null;
    }
}

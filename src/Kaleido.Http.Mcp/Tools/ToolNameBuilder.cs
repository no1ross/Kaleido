using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// Derives MCP tool names from Kaleido capabilities: <c>{service}_{capability}</c> in snake_case,
/// where the capability part is the type name without its <c>Step</c>, <c>QueryContextSource</c>,
/// <c>QueryViewSource</c>, <c>ViewSource</c> or <c>Source</c> suffix
/// (<c>CaptureRequestingProviderStep</c> in service <c>radiology</c> →
/// <c>radiology_capture_requesting_provider</c>).
/// </summary>
/// <remarks>
/// MCP tool names must match <c>[a-zA-Z0-9_-]{1,64}</c>. Kaleido keeps names inside that limit at
/// authoring time: a service name of at most <see cref="ToolNameBuilder.MaxServiceLength"/>
/// characters (KAL2005) and a capability part of at most
/// <see cref="ToolNameBuilder.MaxCapabilityLength"/> characters (KAL2019). A name that still
/// exceeds the limit is cut, given a stable hash suffix and reported (<see cref="ToolName.Truncated"/>).
/// </remarks>
internal interface IToolNameBuilder
{
    /// <summary>The tool name for a capability type in a service.</summary>
    ToolName Build(string service, string capabilityTypeName);
}

/// <summary>A derived tool name, and whether it had to be shortened to fit MCP's limit.</summary>
[ExcludeFromCodeCoverage]
internal sealed record ToolName(string Value, bool Truncated);

internal sealed partial class ToolNameBuilder : IToolNameBuilder
{
    /// <summary>MCP's limit on tool names.</summary>
    public const int MaxLength = 64;

    /// <summary>The service-name budget (enforced for <c>ServiceName</c> literals by KAL2005).</summary>
    public const int MaxServiceLength = 15;

    /// <summary>The capability-part budget (enforced for capability type names by KAL2019).</summary>
    public const int MaxCapabilityLength = 40;

    // Longest first, so "QueryViewSource" is trimmed before "Source".
    private static readonly string[] Suffixes =
        ["QueryContextSource", "QueryViewSource", "ViewSource", "Source", "Step"];

    public ToolName Build(string service, string capabilityTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityTypeName);

        var capability = ToSnakeCase(TrimSuffix(capabilityTypeName));
        var prefix = ToSnakeCase(service ?? string.Empty);

        var name = string.IsNullOrEmpty(prefix)
            ? capability
            : $"{prefix}_{capability}";

        return name.Length <= MaxLength
            ? new ToolName(name, Truncated: false)
            : new ToolName($"{name[..(MaxLength - 9)]}_{Hash(name)}", Truncated: true);
    }

    private static string TrimSuffix(string typeName)
    {
        foreach (var suffix in Suffixes)
        {
            if (typeName.Length > suffix.Length &&
                typeName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return typeName[..^suffix.Length];
            }
        }

        return typeName;
    }

    // The framework's snake_case ("CaptureMriInfo" -> "capture_mri_info", "MRIInfo" -> "mri_info"),
    // then MCP's character rule: anything outside [a-z0-9_] becomes one "_".
    private static string ToSnakeCase(string value) =>
        InvalidCharacters()
            .Replace(JsonNamingPolicy.SnakeCaseLower.ConvertName(value), "_")
            .Trim('_');

    [GeneratedRegex("[^a-z0-9_]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InvalidCharacters();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8].ToLowerInvariant();
}

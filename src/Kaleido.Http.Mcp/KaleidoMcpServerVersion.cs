using System.Reflection;

namespace Kaleido.Http.Mcp;

/// <summary>
/// The version the MCP server reports to clients, resolved once by <c>AddMcp</c> (#463). The options
/// keep what the host supplied; this is what is reported (ADR 0030).
/// </summary>
/// <param name="Value">The reported version.</param>
internal sealed record KaleidoMcpServerVersion(string Value)
{
    // Reported when the host application carries no version at all.
    internal const string Fallback = "0.0.0";

    /// <summary>
    /// <see cref="KaleidoMcpOptions.ServerVersion"/> exactly as given; when it is not set, the host
    /// application's own version: the entry assembly's informational version without its
    /// <c>+commit</c> suffix, else its assembly version, else <see cref="Fallback"/>.
    /// </summary>
    /// <param name="options">Validated options.</param>
    /// <param name="entryAssembly">The host application's entry assembly (<see cref="Assembly.GetEntryAssembly"/>).</param>
    /// <returns>The version to report.</returns>
    public static KaleidoMcpServerVersion Resolve(KaleidoMcpOptions options, Assembly? entryAssembly)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new(options.ServerVersion ?? HostVersion(entryAssembly));
    }

    private static string HostVersion(Assembly? entryAssembly)
    {
        if (entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is { } informational
            && informational.Split('+')[0] is var version
            && !string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        return entryAssembly?.GetName().Version?.ToString() ?? Fallback;
    }
}

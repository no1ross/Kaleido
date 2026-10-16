namespace Kaleido.Observability.OpenTelemetry;

/// <summary>
/// The MCP adapter's ActivitySource and Meter names, duplicated here rather than referencing
/// Kaleido.Http.Mcp so this package does not take an MCP SDK dependency for consumers who do not
/// host the adapter. They are part of the adapter's stable telemetry contract (<c>McpTelemetry</c>).
/// </summary>
internal static class McpTelemetryNames
{
    // Must match McpTelemetry.ActivitySourceName / MeterName in Kaleido.Http.Mcp.
    public const string ActivitySourceName = "Kaleido.Http.Mcp";
    public const string MeterName = "Kaleido.Http.Mcp";
}

/// <summary>
/// Registers only the MCP adapter's telemetry (<c>Kaleido.Http.Mcp</c>) with an OpenTelemetry
/// pipeline the consumer manages. <c>AddKaleidoInstrumentation()</c> registers it together with
/// the rest of Kaleido's.
/// </summary>
public static class McpOpenTelemetryExtensions
{
    /// <summary>Registers the MCP adapter's <c>ActivitySource</c> (tool call and tool listing spans). Signals are emitted only where Kaleido.Http.Mcp is hosted.</summary>
    public static TracerProviderBuilder AddKaleidoMcpInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddSource(
            McpTelemetryNames.ActivitySourceName);
    }

    /// <summary>Registers the MCP adapter's <c>Meter</c> (tool calls, durations, registry reads). Signals are emitted only where Kaleido.Http.Mcp is hosted.</summary>
    public static MeterProviderBuilder AddKaleidoMcpInstrumentation(
        this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(
            McpTelemetryNames.MeterName);
    }
}

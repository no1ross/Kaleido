namespace Kaleido.Observability.OpenTelemetry;

/// <summary>
/// What <c>AddOpenTelemetry()</c> wires: which signals are exported over OTLP and whether the host's
/// own instrumentation is included. Everything is on by default; bound from
/// <c>Kaleido:OpenTelemetry</c>, then the configure delegate applies.
/// </summary>
/// <remarks>
/// <para>
/// Kaleido emits telemetry only for what only Kaleido can observe (the Process and Queryable runtimes,
/// events, stores, the MCP adapter). Host and transport statistics come from the host's own
/// instrumentation - <see cref="HostInstrumentation"/> adds the usual ones for convenience.
/// </para>
/// <para>
/// Logging never depends on OpenTelemetry: Kaleido logs through <c>ILogger</c> only. With
/// <see cref="Logs"/> off, logs still go to the host's other providers (console, Serilog, Seq ...),
/// just not over OTLP.
/// </para>
/// <code>
/// "Kaleido": {
///   "OpenTelemetry": { "Logs": false }
/// }
/// </code>
/// </remarks>
public sealed class KaleidoOpenTelemetryOptions
{
    /// <summary>The configuration section, below <c>Kaleido</c>.</summary>
    public const string SectionName = "OpenTelemetry";

    /// <summary>Export logs (<c>ILogger</c>, with formatted message and scopes) over OTLP. Default true.</summary>
    public bool Logs { get; set; } = true;

    /// <summary>Export traces (Kaleido's activity sources, plus the host's with <see cref="HostInstrumentation"/>) over OTLP. Default true.</summary>
    public bool Traces { get; set; } = true;

    /// <summary>Export metrics (Kaleido's meters with tuned histograms, plus the host's with <see cref="HostInstrumentation"/>) over OTLP. Default true.</summary>
    public bool Metrics { get; set; } = true;

    /// <summary>
    /// Also instrument the host: ASP.NET Core and HttpClient traces and metrics, runtime metrics.
    /// False keeps only Kaleido's own sources and meters. Default true.
    /// </summary>
    public bool HostInstrumentation { get; set; } = true;
}

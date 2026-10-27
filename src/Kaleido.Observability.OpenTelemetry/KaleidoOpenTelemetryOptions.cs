namespace Kaleido.Observability.OpenTelemetry;

/// <summary>
/// What <c>AddOpenTelemetry()</c> wires: which signals are exported over OTLP and whether the host's
/// own instrumentation is included. Everything is on by default; bound from
/// <c>Kaleido:OpenTelemetry</c>, laid over the options passed in code (configuration overrides code, ADR 0030).
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
/// <para>
/// The export and the resource are configured here too (<see cref="Endpoint"/>,
/// <see cref="Protocol"/>, <see cref="Headers"/>, <see cref="ServiceVersion"/>,
/// <see cref="ResourceAttributes"/>). The resource's <c>service.name</c> is the Kaleido
/// <c>ServiceName</c> (no second name to keep in step) and <c>service.instance.id</c> its
/// <c>InstanceId</c>. The standard OpenTelemetry keys
/// (<c>OTEL_EXPORTER_OTLP_*</c>, <c>OTEL_SERVICE_NAME</c>, <c>OTEL_RESOURCE_ATTRIBUTES</c>), set as
/// environment variables or configuration keys, win over these settings, so a deployment can
/// override what <c>appsettings.json</c> says; without either, the SDK defaults apply
/// (<c>http://localhost:4317</c>, gRPC).
/// </para>
/// <code>
/// "Kaleido": {
///   "OpenTelemetry": {
///     "Endpoint": "http://localhost:4317",
///     "Protocol": "grpc",
///     "ResourceAttributes": "deployment.environment=Development,service.version=1.0.0"
///   }
/// }
/// </code>
/// </remarks>
public sealed record KaleidoOpenTelemetryOptions
{
    /// <summary>The configuration section, below <c>Kaleido</c>.</summary>
    public const string SectionName = "OpenTelemetry";

    /// <summary>Export logs (<c>ILogger</c>, with formatted message and scopes) over OTLP. Default true.</summary>
    public bool Logs { get; init; } = true;

    /// <summary>Export traces (Kaleido's activity sources, plus the host's with <see cref="HostInstrumentation"/>) over OTLP. Default true.</summary>
    public bool Traces { get; init; } = true;

    /// <summary>Export metrics (Kaleido's meters with tuned histograms, plus the host's with <see cref="HostInstrumentation"/>) over OTLP. Default true.</summary>
    public bool Metrics { get; init; } = true;

    /// <summary>
    /// Also instrument the host: ASP.NET Core and HttpClient traces and metrics, runtime metrics.
    /// False keeps only Kaleido's own sources and meters. Default true.
    /// </summary>
    public bool HostInstrumentation { get; init; } = true;

    /// <summary>
    /// The OTLP collector's base address (for example <c>http://localhost:4317</c>). With
    /// <c>http/protobuf</c> each signal's path (<c>v1/traces</c>, <c>v1/metrics</c>, <c>v1/logs</c>)
    /// is appended. <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (or a signal's own
    /// <c>OTEL_EXPORTER_OTLP_{SIGNAL}_ENDPOINT</c>) wins. Null: the SDK default.
    /// </summary>
    public Uri? Endpoint { get; init; }

    /// <summary>
    /// The OTLP protocol: <c>grpc</c> or <c>http/protobuf</c>, as in the OpenTelemetry
    /// specification. <c>OTEL_EXPORTER_OTLP_PROTOCOL</c> wins. Null: the SDK default (gRPC).
    /// </summary>
    public string? Protocol { get; init; }

    /// <summary>
    /// Headers sent with every export, as <c>key1=value1,key2=value2</c> (for example a backend's
    /// API key). <c>OTEL_EXPORTER_OTLP_HEADERS</c> wins.
    /// </summary>
    public string? Headers { get; init; }

    /// <summary>The <c>service.version</c> resource attribute, when set.</summary>
    public string? ServiceVersion { get; init; }

    /// <summary>
    /// Further resource attributes, as <c>key1=value1,key2=value2</c> (for example
    /// <c>deployment.environment=Production</c>). An attribute also set in
    /// <c>OTEL_RESOURCE_ATTRIBUTES</c> takes that value.
    /// </summary>
    public string? ResourceAttributes { get; init; }
}

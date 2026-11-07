namespace Kaleido.Observability.OpenTelemetry;

/// <summary>The OTLP signal an exporter sends, for its signal-specific keys and http path.</summary>
internal enum OtlpSignal
{
    Traces,
    Metrics,
    Logs,
}

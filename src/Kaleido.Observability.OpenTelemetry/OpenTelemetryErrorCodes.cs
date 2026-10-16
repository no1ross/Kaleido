namespace Kaleido.Observability.OpenTelemetry;

/// <summary>Codes of the OpenTelemetry provider's startup failures.</summary>
public static class OpenTelemetryErrorCodes
{
    /// <summary>A <c>Kaleido:OpenTelemetry</c> setting cannot be used (unknown protocol, malformed key/value list).</summary>
    public const string InvalidOptions = "otel_invalid_options";
}

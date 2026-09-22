namespace Kaleido.Observability;

/// <summary>
/// Canonical OpenTelemetry tag key names for cross-cutting correlation context fields.
/// These tags are shared across Process, Queryable, and HTTP transport instrumentation.
/// For domain-specific tag keys see <c>ProcessTelemetry</c> and <c>QueryableTelemetry</c>.
/// </summary>
public static class KaleidoTelemetryTags
{
    /// <summary>The unique identifier of the originating request.</summary>
    public const string RequestId = "kaleido.request.id";

    /// <summary>The instance identifier of the processor that handled the request.</summary>
    public const string ProcessorInstanceId = "kaleido.processor.instance_id";

    /// <summary>The service name of this processor instance.</summary>
    public const string ProcessorName = "kaleido.processor.name";

    /// <summary>The service name of the processor that originated the request.</summary>
    public const string SourceProcessor = "kaleido.source.processor";
}

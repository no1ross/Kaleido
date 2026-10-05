namespace Kaleido.Observability;

/// <summary>
/// Canonical OpenTelemetry tag key names for cross-cutting correlation context fields.
/// These tags are shared across Process, Queryable, and HTTP transport instrumentation.
/// For domain-specific tag keys see <c>ProcessorTelemetry</c> and <c>QueryableTelemetry</c>.
/// </summary>
public static class KaleidoTelemetryTags
{
    /// <summary>The unique identifier of the originating request.</summary>
    public const string RequestId = "kaleido.request.id";

    /// <summary>This service's own instance id (<c>KaleidoServiceOptions.InstanceId</c>); never taken from the wire.</summary>
    public const string ProcessorInstanceId = "kaleido.processor.instance_id";

    /// <summary>The service name of this processor instance.</summary>
    public const string ProcessorName = "kaleido.processor.name";

    /// <summary>The processor whose step called this service (per-hop, inbound).</summary>
    public const string CallingProcessor = "kaleido.calling.processor";

    /// <summary>The step in the calling processor that called this service (per-hop, inbound).</summary>
    public const string CallingStep = "kaleido.calling.step";
}

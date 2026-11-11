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

    /// <summary>
    /// The process (one executing instance, a <see cref="System.Guid"/> string) the work belongs to: set on the
    /// Processor execute activity, and on the Queryable, HTTP, MCP and context-store activities when the
    /// process id is known.
    /// </summary>
    public const string ProcessId = "kaleido.process.id";

    /// <summary>This service's own instance id (<c>KaleidoServiceOptions.InstanceId</c>); never taken from the wire.</summary>
    public const string ProcessorInstanceId = "kaleido.processor.instance_id";

    /// <summary>The service name of this processor instance.</summary>
    public const string ProcessorName = "kaleido.processor.name";

    /// <summary>The processor whose step called this service (per-hop, inbound).</summary>
    public const string CallingProcessor = "kaleido.calling.processor";

    /// <summary>The step in the calling processor that called this service (per-hop, inbound).</summary>
    public const string CallingStep = "kaleido.calling.step";

    /// <summary>The front door the request came through (e.g. <c>mcp</c>; end-to-end, informational).</summary>
    public const string Origin = "kaleido.origin";

    /// <summary>The front door's name for what started the request (the MCP tool; end-to-end, informational).</summary>
    public const string OriginTool = "kaleido.origin.tool";

    /// <summary>The application acting for the caller on this hop, from the principal's actor claim.</summary>
    public const string CallerActor = "kaleido.caller.actor";
}

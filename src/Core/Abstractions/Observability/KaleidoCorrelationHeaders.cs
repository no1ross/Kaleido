namespace Kaleido.Observability;

/// <summary>HTTP header names used to propagate Kaleido correlation context between services.</summary>
public static class KaleidoCorrelationHeaders
{
    /// <summary>Carries the unique identifier of the originating request across service hops.</summary>
    public const string RequestId =
        "X-Kaleido-Request-Id";

    /// <summary>Carries the unique identifier of the active processor process.</summary>
    public const string ProcessId =
        "X-Kaleido-Process-Id";

    /// <summary>Carries the instance identifier of the processor that handled the request.</summary>
    public const string ProcessorInstanceId =
        "X-Kaleido-Processor-Instance-Id";

    /// <summary>Carries the service name of the processor that originated the request.</summary>
    public const string SourceProcessor =
        "X-Kaleido-Source-Processor";
}

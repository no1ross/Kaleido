namespace Kaleido.Observability;

public static class KaleidoCorrelationHeaders
{
    public const string RequestId =
        "X-Kaleido-Request-Id";

    public const string ProcessId =
        "X-Kaleido-Process-Id";

    public const string ProcessorInstanceId =
        "X-Kaleido-Processor-Instance-Id";

    public const string SourceProcessor =
        "X-Kaleido-Source-Processor";
}

namespace Kaleido.Http.Client;

internal interface ICorrelationHeaderStamper
{
    void Stamp(HttpRequestMessage request);

    string? Sanitize(string? value);
}

internal sealed class CorrelationHeaderStamper(
    IKaleidoCorrelationContextAccessor correlation)
    : ICorrelationHeaderStamper
{
    // RFC 7230: header field values must be printable US-ASCII (0x20–0x7E).
    // Values longer than this are truncated to avoid oversized headers.
    private const int MaxHeaderValueLength = 256;

    public void Stamp(HttpRequestMessage request)
    {
        var ctx = correlation.Current;

        if (!string.IsNullOrWhiteSpace(ctx.RequestId))
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.RequestId,
                Sanitize(ctx.RequestId));

        if (ctx.ProcessId.HasValue)
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.ProcessId,
                ctx.ProcessId.Value.ToString());

        if (!string.IsNullOrWhiteSpace(ctx.SourceProcessorName))
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.SourceProcessor,
                Sanitize(ctx.SourceProcessorName));

        if (ctx.ProcessorInstanceId.HasValue)
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.ProcessorInstanceId,
                ctx.ProcessorInstanceId.Value.ToString());

        if (!string.IsNullOrWhiteSpace(ctx.StepName))
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.StepName,
                Sanitize(ctx.StepName));
    }

    public string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Keep only printable US-ASCII (0x20–0x7E), strip everything else.
        var sanitized = new string(
            value.Where(c => c >= 0x20 && c <= 0x7E).ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
            return null;

        // Truncate to avoid oversized headers.
        return sanitized.Length <= MaxHeaderValueLength
            ? sanitized
            : sanitized[..MaxHeaderValueLength];
    }
}

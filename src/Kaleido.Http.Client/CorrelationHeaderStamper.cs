namespace Kaleido.Http.Client;

internal interface ICorrelationHeaderStamper
{
    void Stamp(HttpRequestMessage request);
}

internal sealed class CorrelationHeaderStamper(
    IKaleidoCorrelationContextAccessor correlation)
    : ICorrelationHeaderStamper
{
    public void Stamp(HttpRequestMessage request)
    {
        var ctx = correlation.Current;

        if (!string.IsNullOrWhiteSpace(ctx.RequestId))
        {
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.RequestId,
                ctx.RequestId.Sanitize());
        }

        if (ctx.ProcessId.HasValue)
        {
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.ProcessId,
                ctx.ProcessId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(ctx.SourceProcessorName))
        {
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.SourceProcessor,
                ctx.SourceProcessorName.Sanitize());
        }

        if (ctx.ProcessorInstanceId.HasValue)
        {
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.ProcessorInstanceId,
                ctx.ProcessorInstanceId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(ctx.StepName))
        {
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.StepName,
                ctx.StepName.Sanitize());
        }
    }
}

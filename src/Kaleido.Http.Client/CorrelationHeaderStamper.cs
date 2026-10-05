namespace Kaleido.Http.Client;

internal interface ICorrelationHeaderStamper
{
    void Stamp(HttpRequestMessage request);
}

/// <summary>
/// Stamps outbound correlation headers. Request id and process id are forwarded
/// unchanged; calling processor and step are this service and its executing step,
/// sent only when the call is made from inside a step (never forwarded from inbound).
/// </summary>
internal sealed class CorrelationHeaderStamper(
    IKaleidoCorrelationContextAccessor correlation,
    KaleidoServiceOptions serviceOptions)
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

        if (string.IsNullOrWhiteSpace(ctx.ExecutingStepName))
        {
            return;
        }

        request.Headers.TryAddWithoutValidation(
            KaleidoCorrelationHeaders.CallingProcessor,
            serviceOptions.ServiceName.Sanitize());

        request.Headers.TryAddWithoutValidation(
            KaleidoCorrelationHeaders.CallingStep,
            ctx.ExecutingStepName.Sanitize());
    }
}

namespace Kaleido.Http.Client;

internal interface ICorrelationHeaderStamper
{
    void Stamp(HttpRequestMessage request);
}

/// <summary>
/// Stamps outbound correlation headers. Request id and process id are forwarded
/// unchanged (the process id of an <see cref="IKaleidoProcessForwarding"/> scope wins over
/// the inbound one); calling processor and step are this service and its executing step,
/// sent only when the call is made from inside a step (never forwarded from inbound). It also attaches
/// why the call is made and for whom (<see cref="KaleidoCallContext"/>): a call made while handling an
/// inbound request is made for its caller; the credential handler authorizes it as that caller.
/// </summary>
internal sealed class CorrelationHeaderStamper(
    IKaleidoCorrelationContextAccessor correlation,
    IKaleidoProcessForwarding processForwarding,
    IKaleidoInboundCallerAccessor inboundCaller,
    KaleidoServiceOptions serviceOptions)
    : ICorrelationHeaderStamper
{
    public void Stamp(HttpRequestMessage request)
    {
        var ctx = correlation.Current;

        if (inboundCaller.IsInboundRequest)
        {
            request.Options.Set(
                KaleidoCallContext.Key,
                new KaleidoCallContext(KaleidoCallPurpose.OnBehalfOfCaller, inboundCaller.Principal, inboundCaller.Credential));
        }

        if (!string.IsNullOrWhiteSpace(ctx.RequestId))
        {
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.RequestId,
                ctx.RequestId.Sanitize());
        }

        // The front door the request came through, end to end (informational, ADR 0022).
        if (!string.IsNullOrWhiteSpace(ctx.Origin))
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.Origin, ctx.Origin.Sanitize());
        }

        if (!string.IsNullOrWhiteSpace(ctx.OriginTool))
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.OriginTool, ctx.OriginTool.Sanitize());
        }

        if ((processForwarding.ProcessId ?? ctx.ProcessId) is { } processId)
        {
            request.Headers.TryAddWithoutValidation(
                KaleidoCorrelationHeaders.ProcessId,
                processId.ToString());
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

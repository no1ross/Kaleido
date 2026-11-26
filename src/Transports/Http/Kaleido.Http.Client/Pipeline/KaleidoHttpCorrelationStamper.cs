namespace Kaleido.Http.Client.Pipeline;

internal static class KaleidoHttpCorrelationStamper
{
    internal static void StampKaleidoCorrelation(this HttpRequestMessage request, KaleidoCorrelationContext context, Guid? explicitProcessId = null, string? localServiceName = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (context.RequestId.Sanitize() is { } requestId)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, requestId);
        }

        if (context.Origin.Sanitize() is { } origin)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.Origin, origin);
        }

        if (context.OriginTool.Sanitize() is { } originTool)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.OriginTool, originTool);
        }

        if ((explicitProcessId ?? KaleidoProcessForwarding.CurrentProcessId ?? context.ProcessId) is { } processId)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString("D"));
        }

        if (context.ExecutingStepName.Sanitize() is { } step && localServiceName?.Sanitize() is { } service)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.CallingProcessor, service);
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.CallingStep, step);
        }
    }
}

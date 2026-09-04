using Kaleido.Observability;
using Microsoft.AspNetCore.Http;

namespace Kaleido.AspNetCore.Observability;

public static class KaleidoAspNetCoreHeaders
{
    public const string RequestId =
        "X-Kaleido-Request-Id";

    public const string ProcessId =
        "X-Kaleido-Process-Id";

    public const string ProcessorId =
        "X-Kaleido-Processor-Id";

    public const string ProcessorInstanceId =
        "X-Kaleido-Processor-Instance-Id";

}

internal static class KaleidoAspNetCoreCorrelation
{
    public static KaleidoCorrelationContext Create(
        HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new KaleidoCorrelationContext
        {
            RequestId =
                ReadString(
                    context,
                    KaleidoAspNetCoreHeaders.RequestId),

            ProcessId =
                ReadGuid(
                    context,
                    KaleidoAspNetCoreHeaders.ProcessId),

            ProcessorId =
                ReadString(
                    context,
                    KaleidoAspNetCoreHeaders.ProcessorId),

            ProcessorInstanceId =
                ReadGuid(
                    context,
                    KaleidoAspNetCoreHeaders.ProcessorInstanceId)
        };
    }

    private static string? ReadString(
        HttpContext context,
        string headerName)
    {
        var value =
            context.Request.Headers[headerName]
                .ToString();

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static Guid? ReadGuid(
        HttpContext context,
        string headerName)
    {
        var value =
            ReadString(
                context,
                headerName);

        if (value is null)
        {
            return null;
        }

        if (Guid.TryParse(
            value,
            out var guid))
        {
            return guid;
        }

        throw new BadHttpRequestException(
            $"Header '{headerName}' must be a valid GUID.");
    }
}

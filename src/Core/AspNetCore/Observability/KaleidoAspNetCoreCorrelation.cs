using Kaleido.Observability;
using Microsoft.AspNetCore.Http;

namespace Kaleido.AspNetCore.Observability;


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
                    KaleidoCorrelationHeaders.RequestId),

            ProcessId =
                ReadGuid(
                    context,
                    KaleidoCorrelationHeaders.ProcessId),

            ProcessorInstanceId =
                ReadGuid(
                    context,
                    KaleidoCorrelationHeaders.ProcessorInstanceId),

            SourceProcessorName =
                ReadString(
                    context,
                    KaleidoCorrelationHeaders.SourceProcessor)
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

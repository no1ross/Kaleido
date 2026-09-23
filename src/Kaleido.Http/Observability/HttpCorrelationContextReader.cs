using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.Observability;

/// <summary>
/// Reads Kaleido correlation fields from inbound HTTP request headers and
/// maps them to a <see cref="Kaleido.Observability.KaleidoCorrelationContext"/>.
/// String values are sanitized via <see cref="HttpHeaderSanitizerExtensions"/> before use.
/// </summary>
internal static class HttpCorrelationContextReader
{
    public static KaleidoCorrelationContext ReadCorrelationContext(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new KaleidoCorrelationContext
        {
            RequestId =
                ReadString(context, KaleidoCorrelationHeaders.RequestId)
                ?? Guid.NewGuid().ToString(),

            ProcessId =
                ReadGuid(context, KaleidoCorrelationHeaders.ProcessId),

            ProcessorInstanceId =
                ReadGuid(context, KaleidoCorrelationHeaders.ProcessorInstanceId),

            SourceProcessorName =
                ReadString(context, KaleidoCorrelationHeaders.SourceProcessor),

            StepName =
                ReadString(context, KaleidoCorrelationHeaders.StepName)
        };
    }

    private static string? ReadString(HttpContext context, string headerName)
    {
        var raw = context.Request.Headers[headerName].ToString();
        return raw.Sanitize();
    }

    private static Guid? ReadGuid(HttpContext context, string headerName)
    {
        var value = ReadString(context, headerName);

        if (value is null)
        {
            return null;
        }

        if (Guid.TryParse(value, out var guid))
        {
            return guid;
        }

        throw new BadHttpRequestException(
            $"Header '{headerName}' must be a valid GUID.");
    }
}

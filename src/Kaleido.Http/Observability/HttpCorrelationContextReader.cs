using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.Observability;

/// <summary>
/// Reads Kaleido correlation fields from inbound HTTP request headers and
/// maps them to a <see cref="Kaleido.Observability.KaleidoCorrelationContext"/>.
/// String values are sanitized via <see cref="HttpHeaderSanitizerExtensions"/> before use.
/// </summary>
internal static class HttpCorrelationContextReader
{
    /// <summary>
    /// Reads inbound correlation headers into a
    /// <see cref="KaleidoCorrelationContext"/>.
    /// </summary>
    /// <param name="context">The current HTTP request context.</param>
    /// <param name="trustIdentity">
    /// When <c>false</c>, identity-bearing headers are not honored — a fresh
    /// <see cref="KaleidoCorrelationContext.RequestId"/> is generated and the
    /// calling-processor/calling-step headers are ignored.
    /// <c>X-Kaleido-Process-Id</c> is always read — it is a resumable process
    /// handle, not an identity claim.
    /// </param>
    public static KaleidoCorrelationContext ReadCorrelationContext(
        this HttpContext context,
        bool trustIdentity = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new KaleidoCorrelationContext
        {
            RequestId =
                trustIdentity
                    ? ReadString(context, KaleidoCorrelationHeaders.RequestId)
                        ?? Guid.NewGuid().ToString()
                    : Guid.NewGuid().ToString(),

            ProcessId =
                ReadGuid(context, KaleidoCorrelationHeaders.ProcessId),

            CallingProcessorName =
                trustIdentity
                    ? ReadString(context, KaleidoCorrelationHeaders.CallingProcessor)
                    : null,

            CallingStepName =
                trustIdentity
                    ? ReadString(context, KaleidoCorrelationHeaders.CallingStep)
                    : null
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

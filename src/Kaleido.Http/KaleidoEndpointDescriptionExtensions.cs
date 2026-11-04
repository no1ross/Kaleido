using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http;

// Describes what a Kaleido endpoint answers when it fails, for OpenAPI and Swagger (#553, #564): every
// error is a KaleidoErrorResponse, and every endpoint may answer 500 (the exception handler's last resort).
// The endpoint names the other statuses it can answer: 400 and 415 when it reads a body, 401/403 when a
// caller is required, 404, 409, 422, 502, 503.
internal static class KaleidoEndpointDescriptionExtensions
{
    internal static RouteHandlerBuilder ProducesKaleidoErrors(this RouteHandlerBuilder builder, params int[] statusCodes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(statusCodes);

        foreach (var statusCode in statusCodes.Append(StatusCodes.Status500InternalServerError).Distinct().Order())
        {
            builder.Produces<KaleidoErrorResponse>(statusCode, "application/json");
        }

        return builder;
    }
}

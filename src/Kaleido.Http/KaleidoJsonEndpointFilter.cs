using Microsoft.AspNetCore.Http;

namespace Kaleido.Http;

/// <summary>
/// Kaleido-scoped endpoint filter that serializes responses with
/// <see cref="KaleidoJsonOptions.Options"/> — string enums, camelCase property names.
/// Applied at the route-group level so it only affects Kaleido endpoints and
/// leaves the application's global JSON options untouched.
/// </summary>
internal sealed class KaleidoJsonEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var result = await next(context);

        // Wrap value-bearing results so they serialize with Kaleido JSON options.
        // Non-value results (NoContent, Redirect, File, etc.) pass through untouched.
        if (result is IValueHttpResult valueResult)
        {
            var statusCode =
                result is IStatusCodeHttpResult statusResult
                    ? statusResult.StatusCode
                    : StatusCodes.Status200OK;

            return Results.Json(
                valueResult.Value,
                KaleidoJsonOptions.Options,
                statusCode: statusCode);
        }

        return result;
    }
}

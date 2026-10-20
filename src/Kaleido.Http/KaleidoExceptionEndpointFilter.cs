using Kaleido.Http.Middleware;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http;

/// <summary>
/// Kaleido-scoped endpoint filter that answers an exception from a Kaleido endpoint with Kaleido's error
/// response (status code, <see cref="KaleidoErrorResponse"/>, metric, log) inside the endpoint.
/// </summary>
/// <remarks>
/// Kaleido answers its own errors inside its endpoints, so a host's exception handling (the developer
/// exception page <c>WebApplication</c> adds in Development, or <c>UseExceptionHandler</c>) never turns a
/// Kaleido validation error into a 500, and the host's own endpoints are never touched (#210, #295).
/// Applied first on every Kaleido route group, so it wraps the other filters. Without <c>AddHttp()</c>
/// no handler is registered; the exception then propagates unchanged.
/// </remarks>
internal sealed class KaleidoExceptionEndpointFilter(IKaleidoExceptionHandler? handler = null) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception) when (handler is not null)
        {
            await handler.HandleAsync(context.HttpContext, exception, context.HttpContext.RequestAborted);

            // The response is written; nothing more to execute.
            return Results.Empty;
        }
    }
}

using Kaleido.Http.Middleware;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http;

/// <summary>
/// Kaleido-scoped endpoint filter that answers an exception from a Kaleido endpoint with Kaleido's error
/// response (status code, <see cref="KaleidoErrorResponse"/>, metric, log) inside the endpoint.
/// </summary>
/// <remarks>
/// A host's own exception handling - the developer exception page <c>WebApplication</c> adds in
/// Development, or <c>UseExceptionHandler</c> - sits inside the pipeline, closer to the endpoints than
/// Kaleido's <see cref="ExceptionMiddleware"/>. Without this filter it would catch a Kaleido validation
/// error first and answer 500. Applied first on every Kaleido route group, so it wraps the other filters.
/// Without <c>AddHttp()</c> no handler is registered; the exception then propagates unchanged.
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

using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.Middleware;

// The outermost middleware: failures outside Kaleido's endpoints (correlation setup, the rest of the
// pipeline) get Kaleido's error answer. Exceptions from Kaleido's endpoints are already answered
// inside the endpoint (KaleidoExceptionEndpointFilter).
internal sealed class ExceptionMiddleware(RequestDelegate next, IKaleidoExceptionHandler handler)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await handler.HandleAsync(context, exception, context.RequestAborted);
        }
    }
}

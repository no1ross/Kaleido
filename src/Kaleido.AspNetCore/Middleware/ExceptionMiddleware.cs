using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.AspNetCore.Middleware;

internal sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected mid-request — not an error, log at Debug to avoid noise.
            logger.LogDebug("Request was canceled by the client.");
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(
                exception,
                "Invalid argument in request.");

            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            await context.Response.WriteAsJsonAsync(
                new KaleidoErrorResponse(
                [
                    new KaleidoError(KaleidoErrorCodes.ArgumentError, exception.Message)
                ]));
        }
        catch (KaleidoFrameworkException exception)
        {
            logger.LogError(
                exception,
                "Kaleido framework integrity violation.");

            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsJsonAsync(
                new KaleidoErrorResponse(
                [
                    new KaleidoError(KaleidoErrorCodes.FrameworkError, "An internal framework error occurred.")
                ]));
        }
    }
}

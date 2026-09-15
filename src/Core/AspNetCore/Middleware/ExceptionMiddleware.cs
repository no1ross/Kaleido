using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.AspNetCore.Middleware
{
    internal sealed class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ArgumentException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Invalid argument in request.");

                context.Response.StatusCode =
                    StatusCodes.Status400BadRequest;

                await context.Response.WriteAsJsonAsync(
                    new KaleidoErrorResponse(
                    [
                        new KaleidoError("argument_error", exception.Message)
                    ]));
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Invalid operation in request.");

                context.Response.StatusCode =
                    StatusCodes.Status400BadRequest;

                await context.Response.WriteAsJsonAsync(
                    new KaleidoErrorResponse(
                    [
                        new KaleidoError("invalid_operation", exception.Message)
                    ]));
            }
        }
    }
}

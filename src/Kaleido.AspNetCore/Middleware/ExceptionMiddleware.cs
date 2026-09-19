using Kaleido.AspNetCore.Observability;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
            var initializer =
                context.RequestServices
                    .GetService<IKaleidoCorrelationContextInitializer>();

            initializer?.Initialize(
                KaleidoAspNetCoreCorrelation.Create(context));

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
            catch (KaleidoFrameworkException exception)
            {
                _logger.LogError(
                    exception,
                    "Kaleido framework integrity violation.");

                context.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;

                await context.Response.WriteAsJsonAsync(
                    new KaleidoErrorResponse(
                    [
                        new KaleidoError("framework_error", exception.Message)
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

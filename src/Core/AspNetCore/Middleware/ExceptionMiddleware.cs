using Kaleido.AspNetCore.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.AspNetCore.Middleware
{
    public sealed class ExceptionMiddleware
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
                    "Invalid query request.");

                context.Response.StatusCode =
                    StatusCodes.Status400BadRequest;

                await context.Response.WriteAsJsonAsync(
                    new ApiErrorContract(
                        exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Invalid query request.");

                context.Response.StatusCode =
                    StatusCodes.Status400BadRequest;

                await context.Response.WriteAsJsonAsync(
                    new ApiErrorContract(
                        exception.Message));
            }
        }
    }
}

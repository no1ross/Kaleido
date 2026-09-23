using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Kaleido.Http.Middleware;

internal sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private static readonly Meter Meter =
        new(KaleidoHttpTelemetry.MeterName);

    private static readonly Counter<long> EndpointErrorsCounter =
        Meter.CreateCounter<long>(
            KaleidoHttpTelemetry.EndpointErrorsCounterName);

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
        catch (KaleidoValidationException exception)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

            logger.LogWarning(
                exception,
                "Validation failed [{Code}]: {Message}",
                exception.Code,
                exception.Message);

            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            RecordEndpointError(
                exception.Code,
                StatusCodes.Status400BadRequest);

            await context.Response.WriteAsJsonAsync(
                new KaleidoErrorResponse(
                [
                    new KaleidoError(exception.Code, exception.Message)
                ]));
        }
        catch (ArgumentException exception)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

            logger.LogWarning(
                exception,
                "Invalid argument in request.");

            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            RecordEndpointError(
                KaleidoErrorCodes.ArgumentError,
                StatusCodes.Status400BadRequest);

            await context.Response.WriteAsJsonAsync(
                new KaleidoErrorResponse(
                [
                    new KaleidoError(KaleidoErrorCodes.ArgumentError, "An invalid argument was provided.")
                ]));
        }
        catch (KaleidoConfigurationException exception)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

            logger.LogError(
                exception,
                "Kaleido configuration error [{Code}]: {Message}",
                exception.Code,
                exception.Message);

            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            RecordEndpointError(
                exception.Code,
                StatusCodes.Status500InternalServerError);

            await context.Response.WriteAsJsonAsync(
                new KaleidoErrorResponse(
                [
                    new KaleidoError(exception.Code, exception.Message)
                ]));
        }
        catch (KaleidoFrameworkException exception)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

            logger.LogError(
                exception,
                "Kaleido framework integrity violation [{Code}]: {Message}",
                exception.Code,
                exception.Message);

            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            RecordEndpointError(
                exception.Code,
                StatusCodes.Status500InternalServerError);

            await context.Response.WriteAsJsonAsync(
                new KaleidoErrorResponse(
                [
                    new KaleidoError(exception.Code, exception.Message)
                ]));
        }
        catch (Exception exception)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

            logger.LogError(
                exception,
                "Unhandled exception processing request.");

            RecordEndpointError(
                KaleidoErrorCodes.FrameworkError,
                StatusCodes.Status500InternalServerError);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;

                await context.Response.WriteAsJsonAsync(
                    new KaleidoErrorResponse(
                    [
                        new KaleidoError(KaleidoErrorCodes.FrameworkError, "An unexpected error occurred.")
                    ]));
            }
        }
    }

    private static void RecordEndpointError(
        string errorCode,
        int statusCode) =>
        EndpointErrorsCounter.Add(
            1,
            new TagList
            {
                { KaleidoHttpTelemetry.TagErrorCode, errorCode },
                { KaleidoHttpTelemetry.TagHttpStatusCode, statusCode }
            });
}

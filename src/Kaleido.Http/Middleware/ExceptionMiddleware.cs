using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        // The error responses below are written with CancellationToken.None on purpose: they run in
        // catch blocks, and a canceled request is already handled by the OperationCanceledException
        // clause - passing RequestAborted would throw a new cancellation out of a catch block.
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected mid-request — not an error, log at Debug to avoid noise.
            logger.LogDebug("Request was canceled by the client.");
        }
        catch (KaleidoAuthorizationException exception)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

            logger.LogWarning(
                exception,
                "Authorization denied (caller authenticated: {CallerIsAuthenticated}): {Message}",
                exception.CallerIsAuthenticated,
                exception.Message);

            if (context.RequestServices.GetService<IAuthenticationService>() is null)
            {
                context.Response.StatusCode =
                    exception.CallerIsAuthenticated
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status401Unauthorized;
            }
            else if (exception.CallerIsAuthenticated)
            {
                await context.ForbidAsync();
            }
            else
            {
                await context.ChallengeAsync();
            }
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
                ]),
                CancellationToken.None);
        }
        catch (BadHttpRequestException exception)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

            logger.LogWarning(
                exception,
                "Malformed request: {Message}",
                exception.Message);

            context.Response.StatusCode = exception.StatusCode;

            RecordEndpointError(
                KaleidoErrorCodes.ArgumentError,
                exception.StatusCode);

            await context.Response.WriteAsJsonAsync(
                new KaleidoErrorResponse(
                [
                    new KaleidoError(KaleidoErrorCodes.ArgumentError, exception.Message)
                ]),
                CancellationToken.None);
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
                ]),
                CancellationToken.None);
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
                ]),
                CancellationToken.None);
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
                ]),
                CancellationToken.None);
        }
        // Last resort: any other failure becomes a 500 with no details (cancellation is handled above).
        catch (Exception exception) when (exception is not OperationCanceledException)
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
                    ]),
                    CancellationToken.None);
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

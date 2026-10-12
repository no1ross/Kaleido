using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Middleware;

internal sealed partial class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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
            LogRequestCanceled(logger);
        }
        catch (KaleidoAuthorizationException exception)
        {
            MarkFailed(exception);
            LogAuthorizationDenied(logger, exception, exception.CallerIsAuthenticated, exception.Message);
            await DenyAsync(context, exception);
        }
        catch (KaleidoValidationException exception)
        {
            MarkFailed(exception);
            LogValidationFailed(logger, exception, exception.Code, exception.Message);
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
        catch (BadHttpRequestException exception)
        {
            MarkFailed(exception);
            LogMalformedRequest(logger, exception, exception.Message);
            await WriteErrorAsync(context, exception.StatusCode, KaleidoErrorCodes.ArgumentError, exception.Message);
        }
        catch (ArgumentException exception)
        {
            MarkFailed(exception);
            LogInvalidArgument(logger, exception);
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, KaleidoErrorCodes.ArgumentError, "An invalid argument was provided.");
        }
        catch (KaleidoConfigurationException exception)
        {
            MarkFailed(exception);
            LogConfigurationError(logger, exception, exception.Code, exception.Message);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, exception.Code, exception.Message);
        }
        catch (KaleidoFrameworkException exception)
        {
            MarkFailed(exception);
            LogFrameworkError(logger, exception, exception.Code, exception.Message);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, exception.Code, exception.Message);
        }
        // Last resort: any other failure becomes a 500 with no details (cancellation is handled above).
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkFailed(exception);
            LogUnhandled(logger, exception);

            if (context.Response.HasStarted)
            {
                RecordEndpointError(KaleidoErrorCodes.FrameworkError, StatusCodes.Status500InternalServerError);
                return;
            }

            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, KaleidoErrorCodes.FrameworkError, "An unexpected error occurred.");
        }
    }

    private static void MarkFailed(Exception exception) =>
        Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

    // Without an authentication handler the status code is set directly; with one, the handler
    // answers (a challenge for an anonymous caller, forbid for an authenticated one).
    private static async Task DenyAsync(HttpContext context, KaleidoAuthorizationException exception)
    {
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

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;

        RecordEndpointError(code, statusCode);

        await context.Response.WriteAsJsonAsync(
            new KaleidoErrorResponse([new KaleidoError(code, message)]),
            CancellationToken.None);
    }
    private static void RecordEndpointError(
        string errorCode,
        int statusCode) =>
        EndpointErrorsCounter.Add(
            1,
            new TagList
            {
                { KaleidoHttpTelemetry.TagErrorCode, errorCode },
                { KaleidoHttpTelemetry.TagHttpStatusCode, statusCode },
            });

    [LoggerMessage(Level = LogLevel.Debug, Message = "Request was canceled by the client.")]
    private static partial void LogRequestCanceled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization denied (caller authenticated: {CallerIsAuthenticated}): {Message}")]
    private static partial void LogAuthorizationDenied(ILogger logger, Exception exception, bool callerIsAuthenticated, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Validation failed [{Code}]: {Message}")]
    private static partial void LogValidationFailed(ILogger logger, Exception exception, string code, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Malformed request: {Message}")]
    private static partial void LogMalformedRequest(ILogger logger, Exception exception, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid argument in request.")]
    private static partial void LogInvalidArgument(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Kaleido configuration error [{Code}]: {Message}")]
    private static partial void LogConfigurationError(ILogger logger, Exception exception, string code, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Kaleido framework integrity violation [{Code}]: {Message}")]
    private static partial void LogFrameworkError(ILogger logger, Exception exception, string code, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception processing request.")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}

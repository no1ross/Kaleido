using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Middleware;

/// <summary>
/// Turns an exception into Kaleido's HTTP answer: the status code, the <see cref="KaleidoErrorResponse"/>
/// body and the log entry. Used by <see cref="ExceptionMiddleware"/> and by
/// <see cref="KaleidoExceptionEndpointFilter"/>, which answers inside Kaleido's endpoints so a host's own
/// exception handling (the developer exception page, <c>UseExceptionHandler</c>) never sees Kaleido's errors.
/// </summary>
internal interface IKaleidoExceptionHandler
{
    /// <summary>Writes the answer for <paramref name="exception"/> to the response.</summary>
    /// <param name="context">The request that failed.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="cancellationToken">
    /// The request's cancellation (<see cref="HttpContext.RequestAborted"/>). When the client is gone the
    /// error is still logged, but no response is written. The response itself is written
    /// with <see cref="CancellationToken.None"/>, so the error handling never throws a cancellation of
    /// its own.
    /// </param>
    Task HandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IKaleidoExceptionHandler"/>
internal sealed partial class KaleidoExceptionHandler(ILogger<KaleidoExceptionHandler> logger)
    : IKaleidoExceptionHandler
{
    // The error responses are written with CancellationToken.None on purpose: they run after a
    // failure, and a canceled request is handled first - passing RequestAborted would throw a new
    // cancellation out of the error handling. The token only decides whether to write at all.
    public async Task HandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case OperationCanceledException:
                // Client disconnected mid-request — not an error, log at Debug to avoid noise.
                LogRequestCanceled(logger);
                break;

            case KaleidoAuthorizationException authorization:
                MarkFailed(authorization);
                LogAuthorizationDenied(logger, authorization, authorization.CallerIsAuthenticated, authorization.Message);
                await DenyAsync(context, authorization, cancellationToken);
                break;

            case KaleidoValidationException validation:
                MarkFailed(validation);
                LogValidationFailed(logger, validation, validation.Code, validation.Message);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, validation.Code, validation.Message, cancellationToken);
                break;

            case KaleidoConcurrencyException conflict:
                MarkFailed(conflict);
                LogConflict(logger, conflict.ProcessId);
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, conflict.Code, conflict.Message, cancellationToken);
                break;

            case BadHttpRequestException badRequest:
                MarkFailed(badRequest);
                LogMalformedRequest(logger, badRequest, badRequest.Message);
                await WriteErrorAsync(context, badRequest.StatusCode, KaleidoErrorCodes.ArgumentError, badRequest.Message, cancellationToken);
                break;

            case ArgumentException argument:
                MarkFailed(argument);
                LogInvalidArgument(logger, argument);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, KaleidoErrorCodes.ArgumentError, "An invalid argument was provided.", cancellationToken);
                break;

            case KaleidoConfigurationException configuration:
                MarkFailed(configuration);
                LogConfigurationError(logger, configuration, configuration.Code, configuration.Message);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, configuration.Code, configuration.Message, cancellationToken);
                break;

            case KaleidoFrameworkException framework:
                MarkFailed(framework);
                LogFrameworkError(logger, framework, framework.Code, framework.Message);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, framework.Code, framework.Message, cancellationToken);
                break;

            // Last resort: any other failure becomes a 500 with no details.
            default:
                MarkFailed(exception);
                LogUnhandled(logger, exception);
                await WriteUnexpectedErrorAsync(context, cancellationToken);
                break;
        }
    }

    private static void MarkFailed(Exception exception) =>
        Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);

    // Without an authentication handler the status code is set directly; with one, the handler
    // answers (a challenge for an anonymous caller, forbid for an authenticated one).
    private static async Task DenyAsync(HttpContext context, KaleidoAuthorizationException exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

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

    // A 500 without details - unless the response has started or the client is gone.
    private static async Task WriteUnexpectedErrorAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, KaleidoErrorCodes.FrameworkError, "An unexpected error occurred.", cancellationToken);
    }

    // The response is written only while the client is still there.
    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(
            new KaleidoErrorResponse([new KaleidoError(code, message)]),
            CancellationToken.None);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Request was canceled by the client.")]
    private static partial void LogRequestCanceled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authorization denied (caller authenticated: {CallerIsAuthenticated}): {Message}")]
    private static partial void LogAuthorizationDenied(ILogger logger, Exception exception, bool callerIsAuthenticated, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Validation failed [{Code}]: {Message}")]
    private static partial void LogValidationFailed(ILogger logger, Exception exception, string code, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Process {ProcessId} was changed by another request; nothing was saved (409).")]
    private static partial void LogConflict(ILogger logger, Guid processId);

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

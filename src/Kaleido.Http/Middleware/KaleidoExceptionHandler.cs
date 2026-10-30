using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Middleware;

/// <summary>
/// Turns an exception into Kaleido's HTTP answer: the status code, the <see cref="KaleidoErrorResponse"/>
/// body and the log entry. Used by <see cref="KaleidoExceptionEndpointFilter"/>, which answers inside
/// Kaleido's endpoints so a host's own exception handling (the developer exception page,
/// <c>UseExceptionHandler</c>) never sees Kaleido's errors, and the host's own endpoints are never touched.
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
            // The client went away (its request was aborted): not an error, logged at Debug, and nobody
            // is waiting for an answer. Any other OperationCanceledException (a timeout) is a failure,
            // answered 500 below (#517).
            case OperationCanceledException when context.RequestAborted.IsCancellationRequested:
                LogRequestCanceled(logger);
                break;

            case KaleidoAuthorizationException authorization:
                LogAuthorizationDenied(logger, authorization, authorization.CallerIsAuthenticated, authorization.Message);
                await DenyAsync(context, authorization, cancellationToken);
                break;

            case KaleidoValidationException validation:
                LogValidationFailed(logger, validation, validation.Code, validation.Message);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, validation.Code, validation.Message, cancellationToken);
                break;

            case KaleidoConcurrencyException conflict:
                LogConflict(logger, conflict.ProcessId);
                await WriteErrorAsync(context, StatusCodes.Status409Conflict, conflict.Code, conflict.Message, cancellationToken);
                break;

            // A malformed request, including a body that does not fit its type if one ever gets past
            // KaleidoJsonBody: a caller's mistake, never a 500.
            case BadHttpRequestException or System.Text.Json.JsonException:
                LogMalformedRequest(logger, exception, exception.Message);
                await WriteMalformedAsync(context, exception, cancellationToken);
                break;

            case ArgumentException argument:
                LogInvalidArgument(logger, argument);
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, KaleidoErrorCodes.ArgumentError, "An invalid argument was provided.", cancellationToken);
                break;

            // A 500 tells the caller the code, never the detail (#290): the message names internals
            // (registrations, configuration keys, types). The Error log carries it; the code links both.
            case KaleidoConfigurationException configuration:
                MarkFailed(configuration);
                LogConfigurationError(logger, configuration, configuration.Code, configuration.Message);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, configuration.Code, "The service is not configured correctly.", cancellationToken);
                break;

            case KaleidoFrameworkException framework:
                MarkFailed(framework);
                LogFrameworkError(logger, framework, framework.Code, framework.Message);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, framework.Code, "An internal error occurred.", cancellationToken);
                break;

            // Last resort: any other failure becomes a 500 with no details.
            default:
                MarkFailed(exception);
                LogUnhandled(logger, exception);
                await WriteUnexpectedErrorAsync(context, cancellationToken);
                break;
        }
    }

    private static Task WriteMalformedAsync(HttpContext context, Exception exception, CancellationToken cancellationToken) =>
        exception is BadHttpRequestException badRequest
            ? WriteErrorAsync(context, badRequest.StatusCode, KaleidoErrorCodes.ArgumentError, badRequest.Message, cancellationToken)
            : WriteErrorAsync(context, StatusCodes.Status400BadRequest, KaleidoErrorCodes.ArgumentError, "The request body does not fit the expected shape.", cancellationToken);

    // Only a 5xx marks the request's span as an error: a 4xx is the caller's mistake, answered to the
    // caller (OpenTelemetry's HTTP server conventions), so dashboards do not count it as a fault. The
    // status describes the failure by code or exception type, never by its message (#290).
    private static void MarkFailed(Exception exception) =>
        Activity.Current.SetErrorStatus(exception);

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

        // Kaleido's own serializer, never the host's HttpJsonOptions: the error body is a published wire
        // contract ({"errors":[{"code","message"}]}) whatever naming policy the host configures (#443).
        await context.Response.WriteAsJsonAsync(
            new KaleidoErrorResponse([new KaleidoError(code, message)]),
            KaleidoJsonOptions.Options,
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

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Kaleido.Http.Authorization;

/// <summary>
/// Writes <see cref="KaleidoErrorResponse"/> bodies for authorization
/// challenges (401) and denials (403), and records them under the shared
/// endpoint-error counter. Registered by <c>AddHttp()</c> in place of the
/// default <see cref="AuthorizationMiddlewareResultHandler"/>.
/// </summary>
/// <remarks>
/// Applies to every authorization result in the host application — including
/// consumer endpoints — once <c>AddHttp()</c> is used. Only the response body
/// differs from ASP.NET defaults; status codes are unchanged.
/// </remarks>
internal sealed class KaleidoAuthorizationResultHandler
    : IAuthorizationMiddlewareResultHandler
{
    private static readonly Meter Meter =
        new(KaleidoHttpTelemetry.MeterName);

    private static readonly Counter<long> EndpointErrorsCounter =
        Meter.CreateCounter<long>(
            KaleidoHttpTelemetry.EndpointErrorsCounterName);

    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    async Task IAuthorizationMiddlewareResultHandler.HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            RecordEndpointError(
                KaleidoErrorCodes.Unauthorized,
                StatusCodes.Status401Unauthorized);

            await context.Response.WriteAsJsonAsync(
                new KaleidoErrorResponse(
                [
                    new KaleidoError(
                        KaleidoErrorCodes.Unauthorized,
                        "Authentication is required.")
                ]),
                KaleidoJsonOptions.Options);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            RecordEndpointError(
                KaleidoErrorCodes.Forbidden,
                StatusCodes.Status403Forbidden);

            await context.Response.WriteAsJsonAsync(
                new KaleidoErrorResponse(
                [
                    new KaleidoError(
                        KaleidoErrorCodes.Forbidden,
                        "The caller is not authorized for this capability.")
                ]),
                KaleidoJsonOptions.Options);
            return;
        }

        await _defaultHandler.HandleAsync(
            next,
            context,
            policy,
            authorizeResult);
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

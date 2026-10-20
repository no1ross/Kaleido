using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Observability;

/// <summary>
/// Sets up the request's <see cref="KaleidoCorrelationContext"/> on Kaleido's endpoints: reads the
/// <c>X-Kaleido-*</c> headers (identity headers only when trusted), tags the request's trace and echoes
/// the correlation headers on the response.
/// </summary>
/// <remarks>
/// Runs inside endpoint execution, after the host's authentication, so trust is judged against the
/// authenticated caller; only Kaleido's endpoints are touched, never the host's own (#295). Applied
/// after <see cref="KaleidoExceptionEndpointFilter"/>, so a malformed header is answered with
/// Kaleido's 400.
/// </remarks>
internal sealed class KaleidoCorrelationEndpointFilter
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        context.HttpContext.InitializeKaleidoCorrelation();

        return await next(context);
    }
}

internal static class KaleidoCorrelationHttpContextExtensions
{
    /// <summary>
    /// Initializes the request's correlation from the inbound headers, tags the current trace and
    /// echoes the end-to-end correlation headers on the response. Used by
    /// <see cref="KaleidoCorrelationEndpointFilter"/> and by in-host endpoints that are not Kaleido
    /// route groups (the MCP adapter).
    /// </summary>
    /// <exception cref="BadHttpRequestException">A correlation header is malformed (e.g. a non-GUID process id).</exception>
    public static void InitializeKaleidoCorrelation(this HttpContext httpContext)
    {
        var options =
            httpContext.RequestServices.GetService<KaleidoHttpOptions>() ?? new KaleidoHttpOptions();

        var trustIdentity =
            options.TrustCorrelationIdentity?.Invoke(httpContext)
            ?? IsCorrelationIdentityTrustedByDefault(httpContext);

        var correlation = httpContext.ReadCorrelationContext(trustIdentity);

        httpContext.RequestServices
            .GetService<IKaleidoCorrelationContextInitializer>()?
            .Initialize(correlation);

        TagActivity(httpContext, correlation);
        EchoHeaders(httpContext, correlation);
    }

    // Default: a host with no authentication infrastructure has nothing to check callers against,
    // so the headers are trusted; otherwise only an authenticated caller may propagate identity.
    private static bool IsCorrelationIdentityTrustedByDefault(HttpContext httpContext) =>
        httpContext.RequestServices.GetService<IAuthenticationSchemeProvider>() is null
        || httpContext.User.Identity?.IsAuthenticated == true;

    // Tags the request's activity (created by ASP.NET Core instrumentation). The instance id is this
    // service's own, never from the wire.
    private static void TagActivity(HttpContext httpContext, KaleidoCorrelationContext correlation)
    {
        if (Activity.Current is not { } activity)
        {
            return;
        }

        activity.SetTag(KaleidoTelemetryTags.RequestId, correlation.RequestId);
        activity.SetTag(
            KaleidoTelemetryTags.ProcessorInstanceId,
            httpContext.RequestServices.GetService<KaleidoServiceOptions>()?.InstanceId.ToString());
        activity.SetTag(KaleidoTelemetryTags.CallingProcessor, correlation.CallingProcessorName);
        activity.SetTag(KaleidoTelemetryTags.CallingStep, correlation.CallingStepName);

        if (correlation.ProcessId.HasValue)
        {
            activity.SetTag(KaleidoTelemetryTags.ProcessId, correlation.ProcessId.Value.ToString());
        }
    }

    // Echoes only the end-to-end correlation; per-hop identity describes the request, not the response.
    // A response that has already started (an MCP message handled on a long-lived stream) keeps its headers.
    private static void EchoHeaders(HttpContext httpContext, KaleidoCorrelationContext correlation)
    {
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        httpContext.Response.OnStarting(() =>
        {
            var headers = httpContext.Response.Headers;

            headers[KaleidoCorrelationHeaders.RequestId] = correlation.RequestId;

            if (correlation.ProcessId.HasValue)
            {
                headers[KaleidoCorrelationHeaders.ProcessId] = correlation.ProcessId.Value.ToString();
            }

            return Task.CompletedTask;
        });
    }
}

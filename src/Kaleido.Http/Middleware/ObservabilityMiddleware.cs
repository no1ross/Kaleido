using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Middleware;

internal sealed class ObservabilityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var initializer =
            context.RequestServices
                .GetService<IKaleidoCorrelationContextInitializer>();

        var options =
            context.RequestServices
                .GetService<KaleidoHttpOptions>() ?? new KaleidoHttpOptions();

        var trustIdentity =
            options.TrustCorrelationIdentity?.Invoke(context)
            ?? IsCorrelationIdentityTrustedByDefault(context);

        var correlation =
            context.ReadCorrelationContext(trustIdentity);

        initializer?.Initialize(correlation);

        // Tag the correlation on the current Activity (created by ASP.NET Core
        // instrumentation). The instance id is this service's own, never from the wire.
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag(KaleidoTelemetryTags.RequestId, correlation.RequestId);
            activity.SetTag(
                KaleidoTelemetryTags.ProcessorInstanceId,
                context.RequestServices.GetService<KaleidoServiceOptions>()?.InstanceId.ToString());
            activity.SetTag(KaleidoTelemetryTags.CallingProcessor, correlation.CallingProcessorName);
            activity.SetTag(KaleidoTelemetryTags.CallingStep, correlation.CallingStepName);

            if (correlation.ProcessId.HasValue)
            {
                activity.SetTag(ProcessorTelemetry.TagProcessId, correlation.ProcessId.Value.ToString());
            }
        }

        // Echo only the end-to-end correlation on the response; per-hop identity
        // describes the request, not the response.
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers[KaleidoCorrelationHeaders.RequestId] = correlation.RequestId;

            if (correlation.ProcessId.HasValue)
            {
                headers[KaleidoCorrelationHeaders.ProcessId] = correlation.ProcessId.Value.ToString();
            }

            return Task.CompletedTask;
        });

        await next(context);
    }

    // Adaptive default: a host with no authentication infrastructure has
    // nothing to check callers against — trust headers (back-compat).
    // Otherwise only authenticated callers may propagate identity fields.
    private static bool IsCorrelationIdentityTrustedByDefault(
        HttpContext context) =>
        context.RequestServices
            .GetService<IAuthenticationSchemeProvider>() is null
        || context.User.Identity?.IsAuthenticated == true;
}

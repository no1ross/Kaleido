using System.Diagnostics;
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

        var correlation = context.ReadCorrelationContext();

        initializer?.Initialize(correlation);

        // Tag all correlation fields on the current Activity (created by ASP.NET Core
        // instrumentation) so every span for this request carries the full Kaleido context.
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag(KaleidoTelemetryTags.RequestId, correlation.RequestId);
            activity.SetTag(KaleidoTelemetryTags.ProcessorInstanceId, correlation.ProcessorInstanceId?.ToString());
            activity.SetTag(KaleidoTelemetryTags.SourceProcessor, correlation.SourceProcessorName);

            if (correlation.ProcessId.HasValue)
            {
                activity.SetTag(ProcessTelemetry.TagProcessId, correlation.ProcessId.Value.ToString());
            }

            if (!string.IsNullOrWhiteSpace(correlation.StepName))
            {
                activity.SetTag(ProcessTelemetry.TagStepName, correlation.StepName);
            }
        }

        // Echo the full correlation context on the response so callers can correlate
        // requests with backend traces and know exactly where in the process graph
        // this service was called from.
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers[KaleidoCorrelationHeaders.RequestId] = correlation.RequestId;

            if (correlation.ProcessId.HasValue)
            {
                headers[KaleidoCorrelationHeaders.ProcessId] = correlation.ProcessId.Value.ToString();
            }

            if (correlation.ProcessorInstanceId.HasValue)
            {
                headers[KaleidoCorrelationHeaders.ProcessorInstanceId] = correlation.ProcessorInstanceId.Value.ToString();
            }

            if (!string.IsNullOrWhiteSpace(correlation.SourceProcessorName))
            {
                headers[KaleidoCorrelationHeaders.SourceProcessor] = correlation.SourceProcessorName;
            }

            if (!string.IsNullOrWhiteSpace(correlation.StepName))
            {
                headers[KaleidoCorrelationHeaders.StepName] = correlation.StepName;
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}

using Kaleido.Process.Observability;
using Kaleido.Queryable.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Observability.OpenTelemetry;

public static class KaleidoObservabilityOpenTelemetryExtensions
{
    /// <summary>
    /// Wires full OpenTelemetry instrumentation into a Kaleido service in a single call:
    /// structured log export, distributed tracing (Kaleido + ASP.NET Core + HttpClient),
    /// and metrics (Kaleido + ASP.NET Core + HttpClient + runtime). Exports via OTLP.
    /// </summary>
    /// <remarks>
    /// Service name is resolved from the <c>OTEL_SERVICE_NAME</c> configuration key,
    /// falling back to <c>KaleidoServiceOptions.ServiceName</c>.
    /// <para>
    /// For consumers managing their own OpenTelemetry setup, use
    /// <see cref="AddKaleidoInstrumentation(TracerProviderBuilder)"/> and
    /// <see cref="AddKaleidoInstrumentation(MeterProviderBuilder)"/> directly.
    /// </para>
    /// </remarks>
    public static IKaleidoBuilder AddOpenTelemetry(
        this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var serviceName =
            builder.Configuration["OTEL_SERVICE_NAME"]
            ?? builder.ServiceOptions.ServiceName;

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: serviceName))
            .WithLogging(
                logging => logging.AddOtlpExporter(),
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                })
            .WithTracing(tracing => tracing
                .AddKaleidoInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddKaleidoInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        return builder;
    }

    /// <summary>
    /// Registers all Kaleido <see cref="System.Diagnostics.ActivitySource"/>s (Process + Queryable)
    /// with the <see cref="TracerProviderBuilder"/>.
    /// Use this when managing your own OpenTelemetry setup instead of <see cref="AddOpenTelemetry"/>.
    /// </summary>
    public static TracerProviderBuilder AddKaleidoInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddKaleidoProcessInstrumentation()
            .AddKaleidoQueryableInstrumentation();
    }

    /// <summary>
    /// Registers all Kaleido <see cref="System.Diagnostics.Metrics.Meter"/>s (Process + Queryable)
    /// with the <see cref="MeterProviderBuilder"/>, including tuned histogram bucket boundaries.
    /// Use this when managing your own OpenTelemetry setup instead of <see cref="AddOpenTelemetry"/>.
    /// </summary>
    public static MeterProviderBuilder AddKaleidoInstrumentation(
        this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Step-count histograms: values are small integers, typically 1–20.
        // Default OTel boundaries (up to 10,000) produce meaningless percentiles for these.
        var smallCountBoundaries = new ExplicitBucketHistogramConfiguration
        {
            Boundaries = [1, 2, 3, 5, 8, 13, 21]
        };

        // Record-count histograms: queryable result sizes can be larger.
        var recordCountBoundaries = new ExplicitBucketHistogramConfiguration
        {
            Boundaries = [0, 10, 25, 50, 100, 250, 500, 1000]
        };

        // Page size: bounded by typical API page size conventions.
        var pageSizeBoundaries = new ExplicitBucketHistogramConfiguration
        {
            Boundaries = [10, 25, 50, 100, 250, 500]
        };

        // Views must be registered before adding the meter to take effect.
        return builder
            .AddView(ProcessTelemetry.SubmittedStepCountHistogramName, smallCountBoundaries)
            .AddView(ProcessTelemetry.PlanCandidateCountHistogramName, smallCountBoundaries)
            .AddView(ProcessTelemetry.PlanExecutableCountHistogramName, smallCountBoundaries)
            .AddView(QueryableTelemetry.TotalCountHistogramName, recordCountBoundaries)
            .AddView(QueryableTelemetry.ReturnedCountHistogramName, recordCountBoundaries)
            .AddView(QueryableTelemetry.PageSizeHistogramName, pageSizeBoundaries)
            .AddView(QueryableTelemetry.PageOffsetHistogramName, recordCountBoundaries)
            .AddKaleidoProcessInstrumentation()
            .AddKaleidoQueryableInstrumentation();
    }
}

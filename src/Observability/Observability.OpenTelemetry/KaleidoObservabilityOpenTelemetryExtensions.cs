using Kaleido.Eventing;
using Kaleido.Processor.Observability;
using Kaleido.Queryable.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Observability.OpenTelemetry;

/// <summary>
/// Connects Kaleido to OpenTelemetry: <see cref="AddOpenTelemetry"/> is the one-call setup (logs,
/// traces and metrics over OTLP); <see cref="AddKaleidoInstrumentation(TracerProviderBuilder)"/> and
/// <see cref="AddKaleidoInstrumentation(MeterProviderBuilder)"/> register Kaleido's sources and meters
/// with a pipeline the consumer manages.
/// </summary>
public static class KaleidoObservabilityOpenTelemetryExtensions
{
    /// <summary>
    /// Wires OpenTelemetry into a Kaleido service in one call and exports over OTLP: logs, traces
    /// (Kaleido + ASP.NET Core + HttpClient) and metrics (Kaleido + ASP.NET Core + HttpClient +
    /// runtime). Each part can be switched off with <see cref="KaleidoOpenTelemetryOptions"/>:
    /// <paramref name="options"/> from code, with the <c>Kaleido:OpenTelemetry</c> configuration section
    /// laid over a copy of them (configuration overrides code, setting by setting; ADR 0030).
    /// </summary>
    /// <remarks>
    /// The export (endpoint, protocol, headers) and the resource's version and attributes come
    /// from <see cref="KaleidoOpenTelemetryOptions"/>; the standard <c>OTEL_*</c> keys win over
    /// them, and the SDK defaults apply when neither is set. <c>service.name</c> is
    /// <c>KaleidoServiceOptions.ServiceName</c> (<c>OTEL_SERVICE_NAME</c> overrides it) and
    /// <c>service.instance.id</c> is <c>KaleidoServiceOptions.InstanceId</c>.
    /// <para>
    /// For consumers managing their own OpenTelemetry setup, use
    /// <see cref="AddKaleidoInstrumentation(TracerProviderBuilder)"/> and
    /// <see cref="AddKaleidoInstrumentation(MeterProviderBuilder)"/> directly.
    /// </para>
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="options">
    /// Optional options set in code (e.g. switch signals or the host instrumentation off);
    /// <see langword="null"/> uses the defaults.
    /// </param>
    /// <param name="configureTracing">
    /// Optional hook for additional <see cref="TracerProviderBuilder"/> instrumentation
    /// (e.g. EntityFrameworkCore, SQL Client, gRPC) - invoked after the built-in
    /// instrumentations, before the OTLP exporter. Not called when traces are off.
    /// </param>
    /// <param name="configureMetrics">
    /// Optional hook for additional <see cref="MeterProviderBuilder"/> instrumentation -
    /// invoked after the built-in instrumentations, before the OTLP exporter. Not called when
    /// metrics are off.
    /// </param>
    /// <example>
    /// <code>
    /// builder.Services.AddKaleido(builder.Configuration)
    ///     .AddOpenTelemetry(new() { Logs = false });   // traces and metrics over OTLP, logs elsewhere
    /// </code>
    /// </example>
    public static IKaleidoBuilder AddOpenTelemetry(
        this IKaleidoBuilder builder,
        KaleidoOpenTelemetryOptions? options = null,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        options = builder.Configuration
            .GetSection($"{KaleidoServiceOptions.SectionName}:{KaleidoOpenTelemetryOptions.SectionName}")
            .BindOver(options ?? new KaleidoOpenTelemetryOptions(), OpenTelemetryErrorCodes.InvalidOptions);

        if (!options.Logs && !options.Traces && !options.Metrics)
        {
            return builder;
        }

        options.Validate();

        var configuration = builder.Configuration;
        var service = builder.ServiceOptions;

        var telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(r => options.ConfigureResource(r, service));

        if (options.Logs)
        {
            telemetry.WithLogging(
                logging => logging.AddOtlpExporter(o => options.ApplyTo(o, configuration, OtlpSignal.Logs)),
                logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = true;
                });
        }

        if (options.Traces)
        {
            telemetry.WithTracing(tracing => ConfigureTracing(tracing, options, configuration, configureTracing));
        }

        if (options.Metrics)
        {
            telemetry.WithMetrics(metrics => ConfigureMetrics(metrics, options, configuration, configureMetrics));
        }

        return builder;
    }

    private static void ConfigureTracing(TracerProviderBuilder tracing, KaleidoOpenTelemetryOptions options, IConfiguration configuration, Action<TracerProviderBuilder>? configure)
    {
        tracing.AddKaleidoInstrumentation();

        if (options.HostInstrumentation)
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();
        }

        configure?.Invoke(tracing);

        tracing.AddOtlpExporter(o => options.ApplyTo(o, configuration, OtlpSignal.Traces));
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics, KaleidoOpenTelemetryOptions options, IConfiguration configuration, Action<MeterProviderBuilder>? configure)
    {
        metrics.AddKaleidoInstrumentation();

        if (options.HostInstrumentation)
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();
        }

        configure?.Invoke(metrics);

        metrics.AddOtlpExporter(o => options.ApplyTo(o, configuration, OtlpSignal.Metrics));
    }

    /// <summary>
    /// Registers all Kaleido <see cref="System.Diagnostics.ActivitySource"/>s (Process, Queryable,
    /// SQLite store, MCP adapter) with the <see cref="TracerProviderBuilder"/>.
    /// Use this when managing your own OpenTelemetry setup instead of <see cref="AddOpenTelemetry"/>.
    /// </summary>
    public static TracerProviderBuilder AddKaleidoInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddKaleidoProcessorInstrumentation()
            .AddKaleidoQueryableInstrumentation()
            .AddKaleidoSqliteInstrumentation()
            .AddKaleidoMcpInstrumentation();
    }

    /// <summary>
    /// Registers all Kaleido <see cref="System.Diagnostics.Metrics.Meter"/>s (Process, Queryable, events, SQLite store, MCP adapter)
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
            Boundaries = [1, 2, 3, 5, 8, 13, 21],
        };

        // Record-count histograms: queryable result sizes can be larger.
        var recordCountBoundaries = new ExplicitBucketHistogramConfiguration
        {
            Boundaries = [0, 10, 25, 50, 100, 250, 500, 1000],
        };

        // Page size: bounded by typical API page size conventions.
        var pageSizeBoundaries = new ExplicitBucketHistogramConfiguration
        {
            Boundaries = [10, 25, 50, 100, 250, 500],
        };

        // Views must be registered before adding the meter to take effect.
        return builder
            .AddView(ProcessorTelemetry.SubmittedStepCountHistogramName, smallCountBoundaries)
            .AddView(ProcessorTelemetry.PlanCandidateCountHistogramName, smallCountBoundaries)
            .AddView(ProcessorTelemetry.PlanExecutableCountHistogramName, smallCountBoundaries)
            .AddView(QueryableTelemetry.TotalCountHistogramName, recordCountBoundaries)
            .AddView(QueryableTelemetry.ReturnedCountHistogramName, recordCountBoundaries)
            .AddView(QueryableTelemetry.PageSizeHistogramName, pageSizeBoundaries)
            .AddView(QueryableTelemetry.PageOffsetHistogramName, recordCountBoundaries)
            .AddKaleidoProcessorInstrumentation()
            .AddKaleidoQueryableInstrumentation()
            .AddKaleidoSqliteInstrumentation()
            .AddKaleidoMcpInstrumentation()
            .AddMeter(KaleidoEventTelemetry.MeterName);
    }
}

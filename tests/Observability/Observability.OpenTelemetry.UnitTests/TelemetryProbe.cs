using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Kaleido.Observability.OpenTelemetry.UnitTests;

// Whether a built OpenTelemetry provider listens to a source or meter name: a fresh source with that
// name has listeners, a fresh instrument on a meter with that name is enabled.
internal static class TelemetryProbe
{
    public const string Collection = "OpenTelemetry providers";

    public static bool IsTraced(string activitySourceName)
    {
        using var source = new ActivitySource(activitySourceName);
        return source.HasListeners();
    }

    public static bool IsMetered(string meterName)
    {
        using var meter = new Meter(meterName);
        return meter.CreateCounter<long>("kaleido.test.probe").Enabled;
    }

    // The SDK enables instruments only for a provider with a reader; this one exports nowhere.
    public static MeterProvider BuildMeterProvider(Func<MeterProviderBuilder, MeterProviderBuilder> configure) =>
        configure(Sdk.CreateMeterProviderBuilder())
            .AddReader(new PeriodicExportingMetricReader(new NullMetricExporter(), exportIntervalMilliseconds: int.MaxValue))
            .Build();

    private sealed class NullMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch) => ExportResult.Success;
    }
}

// Providers are process-wide listeners: tests that build them run one after another, so a provider
// of one test never answers another test's probe.
[CollectionDefinition(TelemetryProbe.Collection, DisableParallelization = true)]
public sealed class OpenTelemetryProvidersCollection;

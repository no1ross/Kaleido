using Kaleido.Processor.Observability;
using Kaleido.UnitTests;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Kaleido.Observability.OpenTelemetry.UnitTests;

[Collection(TelemetryProbe.Collection)]
public sealed class ProcessorOpenTelemetryExtensionsTests
    : SutFixture
{
    [Fact]
    public void AddKaleidoProcessorInstrumentation_ListensToTheProcessorActivitySource()
    {
        using var provider = Sdk.CreateTracerProviderBuilder().AddKaleidoProcessorInstrumentation().Build();

        Assert.True(TelemetryProbe.IsTraced(ProcessorTelemetry.ActivitySourceName));
    }

    [Fact]
    public void AddKaleidoProcessorInstrumentation_ListensToTheProcessorMeter()
    {
        using var provider = TelemetryProbe.BuildMeterProvider(b => b.AddKaleidoProcessorInstrumentation());

        Assert.True(TelemetryProbe.IsMetered(ProcessorTelemetry.MeterName));
    }
}

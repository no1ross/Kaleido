using Kaleido.Queryable.Observability;
using Kaleido.UnitTests;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Kaleido.Observability.OpenTelemetry.UnitTests;

[Collection(TelemetryProbe.Collection)]
public sealed class QueryableOpenTelemetryExtensionsTests
    : SutFixture
{
    [Fact]
    public void AddKaleidoQueryableInstrumentation_ListensToTheQueryableActivitySource()
    {
        using var provider = Sdk.CreateTracerProviderBuilder().AddKaleidoQueryableInstrumentation().Build();

        Assert.True(TelemetryProbe.IsTraced(QueryableTelemetry.ActivitySourceName));
    }

    [Fact]
    public void AddKaleidoQueryableInstrumentation_ListensToTheQueryableMeter()
    {
        using var provider = TelemetryProbe.BuildMeterProvider(b => b.AddKaleidoQueryableInstrumentation());

        Assert.True(TelemetryProbe.IsMetered(QueryableTelemetry.MeterName));
    }
}

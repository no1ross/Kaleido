using Kaleido.Http.Mcp;
using Kaleido.UnitTests;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Kaleido.Observability.OpenTelemetry.UnitTests;

[Collection(TelemetryProbe.Collection)]
public sealed class McpOpenTelemetryExtensionsTests
    : SutFixture
{
    // The OTel package copies the names (no MCP SDK dependency); they must stay equal to the adapter's.
    [Fact]
    public void TheCopiedNames_EqualTheMcpAdaptersNames()
    {
        Assert.Equal(McpTelemetry.ActivitySourceName, McpTelemetryNames.ActivitySourceName);
        Assert.Equal(McpTelemetry.MeterName, McpTelemetryNames.MeterName);
    }

    [Fact]
    public void AddKaleidoMcpInstrumentation_ListensToTheAdaptersActivitySource()
    {
        using var provider = Sdk.CreateTracerProviderBuilder().AddKaleidoMcpInstrumentation().Build();

        Assert.True(TelemetryProbe.IsTraced(McpTelemetry.ActivitySourceName));
    }

    [Fact]
    public void AddKaleidoMcpInstrumentation_ListensToTheAdaptersMeter()
    {
        using var provider = TelemetryProbe.BuildMeterProvider(b => b.AddKaleidoMcpInstrumentation());

        Assert.True(TelemetryProbe.IsMetered(McpTelemetry.MeterName));
    }
}

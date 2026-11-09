using Kaleido.Provider.SQLite;
using Kaleido.UnitTests;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Kaleido.Observability.OpenTelemetry.UnitTests;

[Collection(TelemetryProbe.Collection)]
public sealed class SqliteOpenTelemetryExtensionsTests
    : SutFixture
{
    // The OTel package copies the names (no EF Core dependency); they must stay equal to the provider's.
    [Fact]
    public void TheCopiedNames_EqualTheSqliteProvidersNames()
    {
        Assert.Equal(SqliteTelemetry.ActivitySourceName, SqliteTelemetryNames.ActivitySourceName);
        Assert.Equal(SqliteTelemetry.MeterName, SqliteTelemetryNames.MeterName);
    }

    [Fact]
    public void AddKaleidoSqliteInstrumentation_ListensToTheProvidersActivitySource()
    {
        using var provider = Sdk.CreateTracerProviderBuilder().AddKaleidoSqliteInstrumentation().Build();

        Assert.True(TelemetryProbe.IsTraced(SqliteTelemetry.ActivitySourceName));
    }

    [Fact]
    public void AddKaleidoSqliteInstrumentation_ListensToTheProvidersMeter()
    {
        using var provider = TelemetryProbe.BuildMeterProvider(b => b.AddKaleidoSqliteInstrumentation());

        Assert.True(TelemetryProbe.IsMetered(SqliteTelemetry.MeterName));
    }
}

using System.Diagnostics.Metrics;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Queryable.Observability;
using Microsoft.Extensions.Logging;

using Kaleido.UnitTests;

namespace Kaleido.Queryable.UnitTests.Observability;

public sealed class QueryableObservabilityTests
    : SutFixture
{
    [Fact]
    public void BeginExecution_WhenDetailsIsNull_Throws()
    {
        var observability = CreateSut();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observability.BeginExecution(null!));

        Assert.Equal(
            "details",
            exception.ParamName);
    }

    [Fact]
    public void Observation_EmitsExpectedMetrics()
    {
        // MeterListener is process-global; isolate this test's measurements by
        // filtering on a unique query context tag so parallel tests don't leak in.
        var contextName = $"test-context-{Guid.NewGuid():N}";
        var observability = CreateSut();

        using var listener = new MeterListener();
        var measurements = new List<(string InstrumentName, long Value)>();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == QueryableTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == QueryableTelemetry.TagQueryContext &&
                    tag.Value as string == contextName)
                {
                    measurements.Add((instrument.Name, measurement));
                }
            }
        });

        listener.Start();

        using var observation =
            observability.BeginExecution(
                new QueryObservationDetails(
                    contextName,
                    "grid",
                    false,
                    QueryExecutionMode.LocalView));

        observation.Materialized(42, 10, 10, 20);
        observation.ValidationFailed(new KaleidoValidationException("qry_test", "bad"));
        observation.Canceled();
        observation.ExecutionFailed(new InvalidOperationException("boom"));

        Assert.Contains(measurements, x => x == ("kaleido.queryable.executions", 1));
        Assert.Contains(measurements, x => x == ("kaleido.queryable.total_count", 42));
        Assert.Contains(measurements, x => x == ("kaleido.queryable.returned_count", 10));
        Assert.Contains(measurements, x => x == ("kaleido.queryable.page_size", 10));
        Assert.Contains(measurements, x => x == ("kaleido.queryable.page_offset", 20));
        Assert.Contains(measurements, x => x == ("kaleido.queryable.validation_failures", 1));
        Assert.Contains(measurements, x => x == ("kaleido.queryable.execution_cancellations", 1));
        Assert.Contains(measurements, x => x == ("kaleido.queryable.execution_failures", 1));
    }

    [Fact]
    public void ValidationFailed_WhenExceptionIsNull_Throws()
    {
        using var observation = BeginExecution();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observation.ValidationFailed(null!));

        Assert.Equal(
            "exception",
            exception.ParamName);
    }

    [Fact]
    public void ExecutionFailed_WhenExceptionIsNull_Throws()
    {
        using var observation = BeginExecution();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observation.ExecutionFailed(null!));

        Assert.Equal(
            "exception",
            exception.ParamName);
    }

    private static IQueryExecutionObservation BeginExecution() =>
        CreateSut().BeginExecution(
            new QueryObservationDetails(
                "test-context",
                null,
                true,
                QueryExecutionMode.DirectContext));

    private static QueryableObservability CreateSut()
    {
        var correlationAccessor =
            new Mock<IKaleidoCorrelationContextAccessor>();

        correlationAccessor
            .SetupGet(x => x.Current)
            .Returns(
                new KaleidoCorrelationContext
                {
                    RequestId = "REQ-001",
                    ProcessId = Guid.NewGuid(),
                    ProcessorInstanceId = Guid.NewGuid(),
                    SourceProcessorName = "source-processor"
                });

        return new QueryableObservability(
            correlationAccessor.Object,
            Mock.Of<ILogger<QueryableObservability>>());
    }
}

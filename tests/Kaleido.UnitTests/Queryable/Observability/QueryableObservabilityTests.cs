using System.Diagnostics.Metrics;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Queryable.Observability;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Queryable.Observability.UnitTests;

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
                if (tag.Key == QueryableTelemetry.TagQuerySource &&
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

    // The caller's query was refused: the transport logs it once at Warning, so the observation logs
    // at Debug only, naming the source (and view) without a "view (null)".
    [Fact]
    public void ValidationFailed_LogsAtDebug_WithTheCapabilityName()
    {
        var logger = new Mock<ILogger<QueryableObservability>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        using (var observation = CreateSut(logger.Object).BeginExecution(new QueryObservationDetails("ProviderSource", null, true, QueryExecutionMode.DirectSource)))
        {
            observation.ValidationFailed(new KaleidoValidationException("MEMBER_PLAN_REQUIRED", "Capture the member first."));
        }

        logger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);

        var logged = logger.Invocations
            .Where(x => x.Method.Name == nameof(ILogger.Log) && Equals(x.Arguments[0], LogLevel.Debug))
            .Select(x => x.Arguments[2].ToString());
        Assert.Contains("Queryable validation failed for ProviderSource with code MEMBER_PLAN_REQUIRED.", logged);
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
                QueryExecutionMode.DirectSource));

    private static QueryableObservability CreateSut(ILogger<QueryableObservability>? logger = null)
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
                    CallingProcessorName = "calling-processor",
                    CallingStepName = "calling-step"
                });

        return new QueryableObservability(
            correlationAccessor.Object,
            new KaleidoServiceOptions { ServiceName = "test-queryable" },
            logger ?? Mock.Of<ILogger<QueryableObservability>>());
    }
}

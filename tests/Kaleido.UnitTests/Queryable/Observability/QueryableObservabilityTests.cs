using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kaleido.Exceptions;
using Kaleido.Observability;
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

    [Fact]
    public void Observation_WithListener_TagsActivitiesAndLogsEachSignal()
    {
        var sourceName = $"test-source-{Guid.NewGuid():N}";
        var processId = Guid.NewGuid();
        var logger = new Mock<ILogger<QueryableObservability>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var activities = new List<Activity>();
        using var listener = CreateActivityListener(activities);
        var childNames = new List<string>();

        using (var observation = CreateSut(logger.Object, processId).BeginExecution(new QueryObservationDetails(sourceName, "grid", false, QueryExecutionMode.LocalView)))
        {
            foreach (var child in new[] { observation.BeginSource(), observation.BeginView(), observation.BeginMaterialization(), observation.BeginDelegate() })
            {
                childNames.Add(Assert.IsType<Activity>(child).OperationName);
                child.Dispose();
            }

            observation.ValidationFailed(new KaleidoValidationException("qry_code", "bad"));
            observation.Materialized(5, 2, 2, 4);
            observation.Canceled();
            observation.ExecutionFailed(new InvalidOperationException("boom"));
        }

        Assert.Equal(
            [QueryableTelemetry.SourceActivityName, QueryableTelemetry.ViewActivityName, QueryableTelemetry.MaterializeActivityName, QueryableTelemetry.DelegateActivityName],
            childNames);
        var activity = Assert.Single(activities, x => x.GetTagItem(QueryableTelemetry.TagQuerySource) as string == sourceName);
        Assert.Equal(QueryableTelemetry.ExecuteActivityName, activity.OperationName);
        Assert.Equal("REQ-001", activity.GetTagItem(KaleidoTelemetryTags.RequestId));
        Assert.Equal("calling-processor", activity.GetTagItem(KaleidoTelemetryTags.CallingProcessor));
        Assert.Equal("calling-step", activity.GetTagItem(KaleidoTelemetryTags.CallingStep));
        Assert.Equal(processId.ToString(), activity.GetTagItem(KaleidoTelemetryTags.ProcessId));
        Assert.Equal("grid", activity.GetTagItem(QueryableTelemetry.TagQueryView));
        Assert.Equal(false, activity.GetTagItem(QueryableTelemetry.TagQueryDirect));
        Assert.Equal(nameof(QueryExecutionMode.LocalView), activity.GetTagItem(QueryableTelemetry.TagQueryExecutionMode));
        Assert.Equal("qry_code", activity.GetTagItem(QueryableTelemetry.TagValidationCode));
        Assert.Equal(5, activity.GetTagItem(QueryableTelemetry.TagTotalCount));
        Assert.Equal(2, activity.GetTagItem(QueryableTelemetry.TagReturnedCount));
        Assert.Equal(2, activity.GetTagItem(QueryableTelemetry.TagPageSize));
        Assert.Equal(4, activity.GetTagItem(QueryableTelemetry.TagPageOffset));
        Assert.Contains(activity.Events, x => x.Name == QueryableTelemetry.CanceledEventName);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(nameof(InvalidOperationException), activity.StatusDescription);

        AssertLogged(logger, LogLevel.Debug, $"Queryable execution started for {sourceName}/grid direct False mode LocalView.");
        AssertLogged(logger, LogLevel.Debug, $"Queryable materialization completed for {sourceName}/grid total 5 returned 2 pageSize 2 pageOffset 4.");
        AssertLogged(logger, LogLevel.Warning, $"Queryable execution was canceled for {sourceName}/grid.");
        AssertLogged(logger, LogLevel.Error, $"Queryable execution failed for {sourceName}/grid.");
    }

    [Fact]
    public void Observation_WithoutListener_ReturnsNoOpChildScopesAndOmitsEmptyPaging()
    {
        var sourceName = $"test-source-{Guid.NewGuid():N}";
        var measurements = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == QueryableTelemetry.MeterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == QueryableTelemetry.TagQuerySource && tag.Value as string == sourceName)
                {
                    lock (measurements)
                    {
                        measurements.Add(instrument.Name);
                    }
                }
            }
        });
        meterListener.Start();

        using (var observation = CreateSut().BeginExecution(new QueryObservationDetails(sourceName, null, true, QueryExecutionMode.DelegatedSource)))
        {
            using var child = observation.BeginDelegate();
            Assert.IsNotType<Activity>(child);
            observation.ValidationFailed(new KaleidoValidationException("qry_code", "bad"));
            observation.Materialized(3, 3, null, null);
            observation.Canceled();
            observation.ExecutionFailed(new InvalidOperationException("boom"));
        }

        Assert.Contains(QueryableTelemetry.TotalCountHistogramName, measurements);
        Assert.DoesNotContain(QueryableTelemetry.PageSizeHistogramName, measurements);
        Assert.DoesNotContain(QueryableTelemetry.PageOffsetHistogramName, measurements);
    }

    private static void AssertLogged(Mock<ILogger<QueryableObservability>> logger, LogLevel level, string message) =>
        Assert.Contains(
            logger.Invocations,
            x => x.Method.Name == nameof(ILogger.Log) &&
                 (LogLevel)x.Arguments[0] == level &&
                 x.Arguments[2].ToString() == message);

    private static ActivityListener CreateActivityListener(List<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == QueryableTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (activities)
                {
                    activities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static IQueryExecutionObservation BeginExecution() =>
        CreateSut().BeginExecution(
            new QueryObservationDetails(
                "test-context",
                null,
                true,
                QueryExecutionMode.DirectSource));

    private static QueryableObservability CreateSut(ILogger<QueryableObservability>? logger = null, Guid? processId = null)
    {
        var correlationAccessor =
            new Mock<IKaleidoCorrelationContextAccessor>();

        correlationAccessor
            .SetupGet(x => x.Current)
            .Returns(
                new KaleidoCorrelationContext
                {
                    RequestId = "REQ-001",
                    ProcessId = processId,
                    CallingProcessorName = "calling-processor",
                    CallingStepName = "calling-step"
                });

        return new QueryableObservability(
            correlationAccessor.Object,
            new KaleidoServiceOptions { ServiceName = "test-queryable" },
            logger ?? Mock.Of<ILogger<QueryableObservability>>());
    }
}

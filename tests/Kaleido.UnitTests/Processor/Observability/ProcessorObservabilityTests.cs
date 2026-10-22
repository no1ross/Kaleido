using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kaleido.Observability;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Processor.Observability.UnitTests;

public sealed class ProcessorObservabilityTests
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
        // filtering on a unique processor.name tag so parallel tests don't leak in.
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var observability = CreateSut(serviceName);

        using var listener = new MeterListener();
        var measurements = new List<(string InstrumentName, long Value)>();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ProcessorTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == KaleidoTelemetryTags.ProcessorName &&
                    tag.Value as string == serviceName)
                {
                    measurements.Add((instrument.Name, measurement));
                }
            }
        });

        listener.Start();

        using var executionObservation =
            observability.BeginExecution(
                new ProcessorExecutionObservationDetails(2));

        executionObservation.ContextInitialized(Guid.NewGuid());
        executionObservation.ContextLoaded(Guid.NewGuid());
        executionObservation.PlanBuilt(3, 2);
        executionObservation.ExecutionFailed(new InvalidOperationException("boom"));

        using var stepObservation =
            observability.BeginStep(
                new ProcessorStepObservationDetails(
                    "Step-A",
                    "1.0.0"));

        stepObservation.Canceled();
        stepObservation.StepFailed(new InvalidOperationException("boom"));

        using var handlerObservation =
            observability.BeginHandler(
                new ProcessorHandlerObservationDetails(
                    "Step-A",
                    "1.0.0"));

        handlerObservation.HandlerFailed(new InvalidOperationException("boom"));

        listener.RecordObservableInstruments();

        Assert.Contains(measurements, x => x == ("kaleido.processor.executions", 1));
        Assert.Contains(measurements, x => x == ("kaleido.processor.submitted_step_count", 2));
        Assert.Contains(measurements, x => x == ("kaleido.processor.contexts_initialized", 1));
        Assert.Contains(measurements, x => x == ("kaleido.processor.contexts_loaded", 1));
        Assert.Contains(measurements, x => x == ("kaleido.processor.plan_candidate_count", 3));
        Assert.Contains(measurements, x => x == ("kaleido.processor.plan_executable_count", 2));
        Assert.Contains(measurements, x => x == ("kaleido.processor.execution_failures", 1));
        Assert.Contains(measurements, x => x == ("kaleido.processor.step_executions", 1));
        Assert.Contains(measurements, x => x == ("kaleido.processor.step_cancellations", 1));
        Assert.Contains(measurements, x => x == ("kaleido.processor.step_failures", 1));
        Assert.Contains(measurements, x => x == ("kaleido.processor.handler_executions", 1));
        Assert.Contains(measurements, x => x == ("kaleido.processor.handler_failures", 1));
    }

    [Fact]
    public void ExecutionRejected_IsNotCountedAsAFailure_AndLogsAtDebug()
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var logger = new Mock<ILogger<ProcessorObservability>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var observability = CreateSut(serviceName, logger.Object);

        using var listener = new MeterListener();
        var failures = 0L;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ProcessorTelemetry.MeterName && instrument.Name == "kaleido.processor.execution_failures")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == KaleidoTelemetryTags.ProcessorName && tag.Value as string == serviceName)
                {
                    failures += measurement;
                }
            }
        });
        listener.Start();

        using (var observation = observability.BeginExecution(new ProcessorExecutionObservationDetails(1)))
        {
            observation.ExecutionRejected(new Kaleido.Exceptions.KaleidoValidationException(ProcessorErrorCodes.ProcessNotFound, "not found"), ProcessorErrorCodes.ProcessNotFound);
        }

        Assert.Equal(0, failures);
        logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
        var rejected = Assert.Single(
            logger.Invocations,
            x => x.Method.Name == nameof(ILogger.Log) && x.Arguments[2].ToString()!.Contains(ProcessorErrorCodes.ProcessNotFound, StringComparison.Ordinal));
        Assert.Equal(LogLevel.Debug, rejected.Arguments[0]);
    }

    [Fact]
    public void BeginStep_WhenDetailsIsNull_Throws()
    {
        var observability = CreateSut();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observability.BeginStep(null!));

        Assert.Equal(
            "details",
            exception.ParamName);
    }

    [Fact]
    public void BeginHandler_WhenDetailsIsNull_Throws()
    {
        var observability = CreateSut();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observability.BeginHandler(null!));

        Assert.Equal(
            "details",
            exception.ParamName);
    }

    [Fact]
    public void ExecutionFailed_WhenExceptionIsNull_Throws()
    {
        var observability = CreateSut();

        using var observation =
            observability.BeginExecution(
                new ProcessorExecutionObservationDetails(1));

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observation.ExecutionFailed(null!));

        Assert.Equal(
            "exception",
            exception.ParamName);
    }

    [Fact]
    public void StepFailed_WhenExceptionIsNull_Throws()
    {
        var observability = CreateSut();

        using var observation =
            observability.BeginStep(
                new ProcessorStepObservationDetails(
                    "Step-A",
                    null));

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observation.StepFailed(null!));

        Assert.Equal(
            "exception",
            exception.ParamName);
    }

    [Fact]
    public void HandlerFailed_WhenExceptionIsNull_Throws()
    {
        var observability = CreateSut();

        using var observation =
            observability.BeginHandler(
                new ProcessorHandlerObservationDetails(
                    "Step-A",
                    null));

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observation.HandlerFailed(null!));

        Assert.Equal(
            "exception",
            exception.ParamName);
    }

    [Fact]
    public void ExecutionObservation_WithListener_TagsActivityAndLogsEachSignal()
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var processId = Guid.NewGuid();
        var logger = CreateEnabledLogger();
        var observability = CreateSut(serviceName, logger.Object);
        var activities = new List<Activity>();
        using var listener = CreateActivityListener(activities);

        using (var observation = observability.BeginExecution(new ProcessorExecutionObservationDetails(2)))
        {
            observation.ContextInitialized(processId);
            observation.ContextLoaded(processId);
            observation.PlanBuilt(3, 1);
            observation.ExecutionRejected(new InvalidOperationException("refused"), "rej_code");
            observation.InformationResponseRejected("Step-A", "info_code");
            observation.ExecutionCompleted(ProcessExecutionState.Complete);
            observation.ExecutionFailed(new InvalidOperationException("boom"));
        }

        var activity = Assert.Single(activities, x => x.GetTagItem(KaleidoTelemetryTags.ProcessorName) as string == serviceName);
        Assert.Equal(ProcessorTelemetry.ExecuteActivityName, activity.OperationName);
        Assert.Equal("REQ-001", activity.GetTagItem(KaleidoTelemetryTags.RequestId));
        Assert.Equal("calling-processor", activity.GetTagItem(KaleidoTelemetryTags.CallingProcessor));
        Assert.Equal("calling-step", activity.GetTagItem(KaleidoTelemetryTags.CallingStep));
        Assert.Equal(processId.ToString(), activity.GetTagItem(KaleidoTelemetryTags.ProcessId));
        Assert.Equal(2, activity.GetTagItem(ProcessorTelemetry.TagSubmittedStepCount));
        Assert.Equal(3, activity.GetTagItem(ProcessorTelemetry.TagPlanCandidateCount));
        Assert.Equal(1, activity.GetTagItem(ProcessorTelemetry.TagPlanExecutableCount));
        Assert.Equal(nameof(ProcessExecutionState.Complete), activity.GetTagItem(ProcessorTelemetry.TagExecutionStatus));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(nameof(InvalidOperationException), activity.StatusDescription);
        var events = activity.Events.Select(x => x.Name).ToList();
        Assert.Contains(ProcessorTelemetry.ContextInitializedEventName, events);
        Assert.Contains(ProcessorTelemetry.ContextLoadedEventName, events);
        Assert.Contains(ProcessorTelemetry.ExecutionCompletedEventName, events);
        var rejected = Assert.Single(activity.Events, x => x.Name == ProcessorTelemetry.ExecutionRejectedEventName);
        Assert.Equal("rej_code", rejected.Tags.Single(x => x.Key == ProcessorTelemetry.TagRejectionCode).Value);

        AssertLogged(logger, LogLevel.Debug, "Process execution started");
        AssertLogged(logger, LogLevel.Debug, "Process context initialized");
        AssertLogged(logger, LogLevel.Debug, "Process context loaded");
        AssertLogged(logger, LogLevel.Debug, "with 3 candidates and 1 executable steps");
        AssertLogged(logger, LogLevel.Debug, "was refused [rej_code]: refused");
        AssertLogged(logger, LogLevel.Debug, "Information response rejected");
        AssertLogged(logger, LogLevel.Information, "with state Complete");
        AssertLogged(logger, LogLevel.Error, "Process execution failed");
    }

    [Fact]
    public void BeginExecution_WithoutProcessIdOrCallingProcessor_OmitsThoseTags()
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var observability = CreateSut(serviceName, correlation: new KaleidoCorrelationContext { RequestId = "REQ-002" });
        var activities = new List<Activity>();
        using var listener = CreateActivityListener(activities);
        var measurements = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = CreateMeterListener(serviceName, measurements);

        observability.BeginExecution(new ProcessorExecutionObservationDetails(0)).Dispose();

        var activity = Assert.Single(activities, x => x.GetTagItem(KaleidoTelemetryTags.ProcessorName) as string == serviceName);
        Assert.Null(activity.GetTagItem(KaleidoTelemetryTags.ProcessId));
        Assert.Null(activity.GetTagItem(KaleidoTelemetryTags.CallingProcessor));
        var executions = Assert.Single(measurements, x => x.Name == ProcessorTelemetry.ExecutionsCounterName);
        Assert.DoesNotContain(executions.Tags, x => x.Key == KaleidoTelemetryTags.CallingProcessor);
    }

    [Fact]
    public void BeginExecution_WithCallingProcessor_TagsMetrics()
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var observability = CreateSut(serviceName);
        var measurements = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = CreateMeterListener(serviceName, measurements);

        observability.BeginExecution(new ProcessorExecutionObservationDetails(1)).Dispose();

        var executions = Assert.Single(measurements, x => x.Name == ProcessorTelemetry.ExecutionsCounterName);
        Assert.Contains(executions.Tags, x => x.Key == KaleidoTelemetryTags.CallingProcessor && (string?)x.Value == "calling-processor");
    }

    [Fact]
    public void InformationResponseRejected_CountsWithStepAndCodeTags()
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var observability = CreateSut(serviceName);
        var measurements = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = CreateMeterListener(serviceName, measurements);

        using (var observation = observability.BeginExecution(new ProcessorExecutionObservationDetails(1)))
        {
            observation.InformationResponseRejected("Step-A", "info_code");
            observation.ExecutionCompleted(ProcessExecutionState.AwaitingInformation);
        }

        var rejected = Assert.Single(measurements, x => x.Name == ProcessorTelemetry.InformationResponsesRejectedCounterName);
        Assert.Contains(rejected.Tags, x => x.Key == ProcessorTelemetry.TagStepName && (string?)x.Value == "Step-A");
        Assert.Contains(rejected.Tags, x => x.Key == ProcessorTelemetry.TagRejectionCode && (string?)x.Value == "info_code");
        Assert.Contains(measurements, x => x.Name == ProcessorTelemetry.ExecutionDurationHistogramName);
    }

    [Fact]
    public void StepObservation_WithListener_TagsActivityAndLogsEachSignal()
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var stepName = $"Step-{Guid.NewGuid():N}";
        var logger = CreateEnabledLogger();
        var observability = CreateSut(serviceName, logger.Object);
        var activities = new List<Activity>();
        using var listener = CreateActivityListener(activities);
        var measurements = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = CreateMeterListener(serviceName, measurements);

        using (var observation = observability.BeginStep(new ProcessorStepObservationDetails(stepName, "2.0.0", IsInformationStep: true)))
        {
            observation.DecisionRecorded("Success", "Completed");
            observation.InformationRequested("REQ-INFO");
            observation.Canceled();
            observation.StepFailed(new InvalidOperationException("step boom"));
        }

        var activity = Assert.Single(activities, x => x.GetTagItem(ProcessorTelemetry.TagStepName) as string == stepName);
        Assert.Equal(ProcessorTelemetry.StepActivityName, activity.OperationName);
        Assert.Equal("2.0.0", activity.GetTagItem(ProcessorTelemetry.TagStepVersion));
        Assert.Equal("information", activity.GetTagItem(ProcessorTelemetry.TagStepKind));
        Assert.Equal("Success", activity.GetTagItem(ProcessorTelemetry.TagDecisionType));
        Assert.Equal("Completed", activity.GetTagItem(ProcessorTelemetry.TagExecutionStatus));
        Assert.Equal("REQ-INFO", activity.GetTagItem(ProcessorTelemetry.TagInformationRequestId));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        var events = activity.Events.Select(x => x.Name).ToList();
        Assert.Contains(ProcessorTelemetry.InformationRequestedEventName, events);
        Assert.Contains(ProcessorTelemetry.StepCanceledEventName, events);

        var requested = Assert.Single(measurements, x => x.Name == ProcessorTelemetry.InformationRequestsCounterName);
        Assert.Contains(requested.Tags, x => x.Key == ProcessorTelemetry.TagStepVersion && (string?)x.Value == "2.0.0");
        Assert.Contains(measurements, x => x.Name == ProcessorTelemetry.StepDurationHistogramName);

        AssertLogged(logger, LogLevel.Debug, "Process step execution started");
        AssertLogged(logger, LogLevel.Debug, "decision Success status Completed");
        AssertLogged(logger, LogLevel.Debug, "Information request REQ-INFO presented");
        AssertLogged(logger, LogLevel.Warning, "Process step execution was canceled");
        AssertLogged(logger, LogLevel.Error, "Process step execution failed");
    }

    [Fact]
    public void StepObservation_StepValidationFailed_IsRecordedAsACallerErrorNotAFailure()
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var stepName = $"Step-{Guid.NewGuid():N}";
        var logger = CreateEnabledLogger();
        var observability = CreateSut(serviceName, logger.Object);
        var activities = new List<Activity>();
        using var listener = CreateActivityListener(activities);
        var measurements = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = CreateMeterListener(serviceName, measurements);

        using (var observation = observability.BeginStep(new ProcessorStepObservationDetails(stepName, "1.0.0")))
        {
            observation.StepValidationFailed("pro_custom_validation");
        }

        var activity = Assert.Single(activities, x => x.GetTagItem(ProcessorTelemetry.TagStepName) as string == stepName);
        Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
        var refused = Assert.Single(activity.Events, x => x.Name == ProcessorTelemetry.StepValidationFailedEventName);
        Assert.Contains(refused.Tags, x => x.Key == ProcessorTelemetry.TagRejectionCode && (string?)x.Value == "pro_custom_validation");
        Assert.DoesNotContain(measurements, x => x.Name == ProcessorTelemetry.StepFailuresCounterName);
        AssertLogged(logger, LogLevel.Debug, "pro_custom_validation");
        logger.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void StepObservation_WithoutVersion_OmitsTheVersionMetricTag(string? version)
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var stepName = $"Step-{Guid.NewGuid():N}";
        var observability = CreateSut(serviceName);
        var activities = new List<Activity>();
        using var listener = CreateActivityListener(activities);
        var measurements = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = CreateMeterListener(serviceName, measurements);

        using (var observation = observability.BeginStep(new ProcessorStepObservationDetails(stepName, version)))
        {
            observation.Canceled();
        }

        var activity = Assert.Single(activities, x => x.GetTagItem(ProcessorTelemetry.TagStepName) as string == stepName);
        Assert.Equal("properties", activity.GetTagItem(ProcessorTelemetry.TagStepKind));
        var canceled = Assert.Single(measurements, x => x.Name == ProcessorTelemetry.StepCancellationsCounterName);
        Assert.DoesNotContain(canceled.Tags, x => x.Key == ProcessorTelemetry.TagStepVersion);
        Assert.DoesNotContain(measurements, x => x.Name == ProcessorTelemetry.StepFailuresCounterName);
    }

    [Fact]
    public void StepObservation_WithoutListener_StillRecordsMetrics()
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var observability = CreateSut(serviceName);
        var measurements = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = CreateMeterListener(serviceName, measurements);

        using (var observation = observability.BeginStep(new ProcessorStepObservationDetails("Step-B", "1.0.0")))
        {
            observation.DecisionRecorded("Failure", "Failed");
            observation.InformationRequested("REQ-B");
            observation.StepFailed(new InvalidOperationException("boom"));
        }

        Assert.Contains(measurements, x => x.Name == ProcessorTelemetry.InformationRequestsCounterName);
        Assert.Contains(measurements, x => x.Name == ProcessorTelemetry.StepFailuresCounterName);
    }

    [Fact]
    public void HandlerObservation_WithListener_TagsActivityAndLogs()
    {
        var serviceName = $"test-processor-{Guid.NewGuid():N}";
        var stepName = $"Step-{Guid.NewGuid():N}";
        var logger = CreateEnabledLogger();
        var observability = CreateSut(serviceName, logger.Object);
        var activities = new List<Activity>();
        using var listener = CreateActivityListener(activities);

        using (var observation = observability.BeginHandler(new ProcessorHandlerObservationDetails(stepName, "3.0.0")))
        {
            observation.HandlerFailed(new InvalidOperationException("handler boom"));
        }

        var activity = Assert.Single(activities, x => x.GetTagItem(ProcessorTelemetry.TagStepName) as string == stepName);
        Assert.Equal(ProcessorTelemetry.StepHandlerActivityName, activity.OperationName);
        Assert.Equal("3.0.0", activity.GetTagItem(ProcessorTelemetry.TagStepVersion));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(nameof(InvalidOperationException), activity.StatusDescription);
        AssertLogged(logger, LogLevel.Trace, "Process handler execution started");
        AssertLogged(logger, LogLevel.Error, "Process handler execution failed");
    }

    private static Mock<ILogger<ProcessorObservability>> CreateEnabledLogger()
    {
        var logger = new Mock<ILogger<ProcessorObservability>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return logger;
    }

    private static void AssertLogged(Mock<ILogger<ProcessorObservability>> logger, LogLevel level, string fragment) =>
        Assert.Contains(
            logger.Invocations,
            x => x.Method.Name == nameof(ILogger.Log) &&
                 (LogLevel)x.Arguments[0] == level &&
                 x.Arguments[2].ToString()!.Contains(fragment, StringComparison.Ordinal));

    private static ActivityListener CreateActivityListener(List<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ProcessorTelemetry.ActivitySourceName,
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

    private static MeterListener CreateMeterListener(
        string serviceName,
        List<(string Name, KeyValuePair<string, object?>[] Tags)> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == ProcessorTelemetry.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };

        void Record(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var copy = tags.ToArray();
            if (copy.Any(x => x.Key == KaleidoTelemetryTags.ProcessorName && x.Value as string == serviceName))
            {
                lock (measurements)
                {
                    measurements.Add((instrument.Name, copy));
                }
            }
        }

        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => Record(instrument, tags));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Record(instrument, tags));
        listener.Start();
        return listener;
    }

    private static ProcessorObservability CreateSut(
        string serviceName = "test-processor",
        ILogger<ProcessorObservability>? logger = null,
        KaleidoCorrelationContext? correlation = null)
    {
        var correlationAccessor =
            new Mock<IKaleidoCorrelationContextAccessor>();

        correlationAccessor
            .SetupGet(x => x.Current)
            .Returns(
                correlation ?? new KaleidoCorrelationContext
                {
                    RequestId = "REQ-001",
                    ProcessId = Guid.NewGuid(),
                    CallingProcessorName = "calling-processor",
                    CallingStepName = "calling-step"
                });

        return new ProcessorObservability(
            correlationAccessor.Object,
            new KaleidoServiceOptions { ServiceName = serviceName, DisplayName = "Test Processor" },
            logger ?? Mock.Of<ILogger<ProcessorObservability>>());
    }
}

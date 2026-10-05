using System.Diagnostics.Metrics;
using Kaleido.Observability;
using Kaleido.Processor.Observability;
using Microsoft.Extensions.Logging;

using Kaleido.UnitTests;

namespace Kaleido.Processor.UnitTests.Observability;

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

    private static ProcessorObservability CreateSut(
        string serviceName = "test-processor")
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

        return new ProcessorObservability(
            correlationAccessor.Object,
            new KaleidoServiceOptions { ServiceName = serviceName, DisplayName = "Test Processor" },
            Mock.Of<ILogger<ProcessorObservability>>());
    }
}

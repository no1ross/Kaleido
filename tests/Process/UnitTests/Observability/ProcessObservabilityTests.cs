using Kaleido.Observability;
using Kaleido.Process.Observability;
using Kaleido.Process.Registry;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.Metrics;

namespace Kaleido.Process.UnitTests.Observability;

public sealed class ProcessObservabilityTests
{
    [Fact]
    public void Constructor_WhenCorrelationAccessorIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessObservability(
                    null!,
                    CreateProcessorRegistry(),
                    Mock.Of<ILogger<ProcessObservability>>()));

        Assert.Equal(
            "correlationAccessor",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenProcessorRegistryIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessObservability(
                    Mock.Of<IKaleidoCorrelationContextAccessor>(),
                    null!,
                    Mock.Of<ILogger<ProcessObservability>>()));

        Assert.Equal(
            "processorRegistry",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessObservability(
                    Mock.Of<IKaleidoCorrelationContextAccessor>(),
                    CreateProcessorRegistry(),
                    null!));

        Assert.Equal(
            "logger",
            exception.ParamName);
    }

    [Fact]
    public void BeginExecution_WhenDetailsIsNull_Throws()
    {
        var observability = CreateObservability();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observability.BeginExecution(null!));

        Assert.Equal(
            "details",
            exception.ParamName);
    }

    [Fact]
    public void BeginExecution_ReturnsObservationThatAcceptsExecutionEvents()
    {
        var observability = CreateObservability();

        using var observation =
            observability.BeginExecution(
                new ProcessExecutionObservationDetails(2));

        observation.ContextInitialized(Guid.NewGuid());
        observation.ContextLoaded(Guid.NewGuid());
        observation.PlanBuilt(3, 2);
        observation.ExecutionFailed(new InvalidOperationException("boom"));
    }

    [Fact]
    public void Observation_EmitsExpectedMetrics()
    {
        using var listener = new MeterListener();
        var measurements = new List<(string InstrumentName, long Value)>();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ProcessTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            measurements.Add((instrument.Name, measurement));
        });

        listener.Start();

        var observability = CreateObservability();

        using var executionObservation =
            observability.BeginExecution(
                new ProcessExecutionObservationDetails(2));

        executionObservation.ContextInitialized(Guid.NewGuid());
        executionObservation.ContextLoaded(Guid.NewGuid());
        executionObservation.PlanBuilt(3, 2);
        executionObservation.ExecutionFailed(new InvalidOperationException("boom"));

        using var stepObservation =
            observability.BeginStep(
                new ProcessStepObservationDetails(
                    "Step-A",
                    "1.0.0"));

        stepObservation.Canceled();
        stepObservation.StepFailed(new InvalidOperationException("boom"));

        using var handlerObservation =
            observability.BeginHandler(
                new ProcessHandlerObservationDetails(
                    "Step-A",
                    "1.0.0"));

        handlerObservation.HandlerFailed(new InvalidOperationException("boom"));

        listener.RecordObservableInstruments();

        Assert.Contains(measurements, x => x == ("kaleido.process.executions", 1));
        Assert.Contains(measurements, x => x == ("kaleido.process.submitted_step_count", 2));
        Assert.Contains(measurements, x => x == ("kaleido.process.contexts_initialized", 1));
        Assert.Contains(measurements, x => x == ("kaleido.process.contexts_loaded", 1));
        Assert.Contains(measurements, x => x == ("kaleido.process.plan_candidate_count", 3));
        Assert.Contains(measurements, x => x == ("kaleido.process.plan_executable_count", 2));
        Assert.Contains(measurements, x => x == ("kaleido.process.execution_failures", 1));
        Assert.Contains(measurements, x => x == ("kaleido.process.step_executions", 1));
        Assert.Contains(measurements, x => x == ("kaleido.process.step_cancellations", 1));
        Assert.Contains(measurements, x => x == ("kaleido.process.step_failures", 1));
        Assert.Contains(measurements, x => x == ("kaleido.process.handler_executions", 1));
        Assert.Contains(measurements, x => x == ("kaleido.process.handler_failures", 1));
    }

    [Fact]
    public void BeginStep_WhenDetailsIsNull_Throws()
    {
        var observability = CreateObservability();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observability.BeginStep(null!));

        Assert.Equal(
            "details",
            exception.ParamName);
    }

    [Fact]
    public void BeginStep_ReturnsObservationThatAcceptsStepEvents()
    {
        var observability = CreateObservability();

        using var observation =
            observability.BeginStep(
                new ProcessStepObservationDetails(
                    "Step-A",
                    "1.0.0"));

        observation.DecisionRecorded(
            "Complete",
            "Completed");

        observation.Canceled();
        observation.StepFailed(new InvalidOperationException("boom"));
    }

    [Fact]
    public void BeginHandler_WhenDetailsIsNull_Throws()
    {
        var observability = CreateObservability();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observability.BeginHandler(null!));

        Assert.Equal(
            "details",
            exception.ParamName);
    }

    [Fact]
    public void BeginHandler_ReturnsObservationThatAcceptsHandlerFailures()
    {
        var observability = CreateObservability();

        using var observation =
            observability.BeginHandler(
                new ProcessHandlerObservationDetails(
                    "Step-A",
                    "1.0.0"));

        observation.HandlerFailed(
            new InvalidOperationException("boom"));
    }

    [Fact]
    public void ExecutionFailed_WhenExceptionIsNull_Throws()
    {
        var observability = CreateObservability();

        using var observation =
            observability.BeginExecution(
                new ProcessExecutionObservationDetails(1));

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
        var observability = CreateObservability();

        using var observation =
            observability.BeginStep(
                new ProcessStepObservationDetails(
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
        var observability = CreateObservability();

        using var observation =
            observability.BeginHandler(
                new ProcessHandlerObservationDetails(
                    "Step-A",
                    null));

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observation.HandlerFailed(null!));

        Assert.Equal(
            "exception",
            exception.ParamName);
    }

    private static ProcessObservability CreateObservability()
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

        return new ProcessObservability(
            correlationAccessor.Object,
            CreateProcessorRegistry(),
            Mock.Of<ILogger<ProcessObservability>>());
    }

    private static IProcessorRegistry CreateProcessorRegistry()
    {
        var registry = new Mock<IProcessorRegistry>();

        registry
            .Setup(x => x.Registrations)
            .Returns(
            [
                new ProcessorRegistryItem
                {
                    Name = "test-processor",
                    Description = "Test processor",
                    Version = "1.0.0",
                    DisplayName = "Test Processor",
                    InitialSteps = [],
                    Steps = []
                }
            ]);

        return registry.Object;
    }
}

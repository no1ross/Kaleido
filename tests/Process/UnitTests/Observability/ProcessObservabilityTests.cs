using Kaleido.Observability;
using Kaleido.Process.Observability;
using Microsoft.Extensions.Logging;
using Moq;

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
                    Mock.Of<ILogger<ProcessObservability>>()));

        Assert.Equal(
            "correlationAccessor",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessObservability(
                    Mock.Of<IKaleidoCorrelationContextAccessor>(),
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
                    ParticipantId = "participant-a",
                    ParticipantInstanceId = Guid.NewGuid(),
                    OrchestratorId = "orchestrator-a",
                    OrchestratorInstanceId = Guid.NewGuid()
                });

        return new ProcessObservability(
            correlationAccessor.Object,
            Mock.Of<ILogger<ProcessObservability>>());
    }
}

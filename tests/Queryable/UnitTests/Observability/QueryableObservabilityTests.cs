using Kaleido.Observability;
using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Observability;
using Microsoft.Extensions.Logging;
using Moq;

namespace Kaleido.Queryable.UnitTests.Observability;

public sealed class QueryableObservabilityTests
{
    [Fact]
    public void Constructor_WhenCorrelationAccessorIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new QueryableObservability(
                    null!,
                    Mock.Of<ILogger<QueryableObservability>>()));

        Assert.Equal(
            "correlationAccessor",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new QueryableObservability(
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
    public void BeginExecution_ReturnsObservationThatSupportsLifecycleMethods()
    {
        var observability = CreateObservability();

        using var observation =
            observability.BeginExecution(
                new QueryObservationDetails(
                    "TestContext",
                    "TestView",
                    false));

        using var sourceScope = observation.BeginSource();
        using var viewScope = observation.BeginView();
        using var materializationScope = observation.BeginMaterialization();

        observation.Materialized(10, 5, 5, 0);
        observation.ValidationFailed(new InvalidFieldException("Field-A", "TestContext"));
        observation.ExecutionFailed(new InvalidOperationException("boom"));
    }

    [Fact]
    public void ValidationFailed_WhenExceptionIsNull_Throws()
    {
        var observability = CreateObservability();

        using var observation =
            observability.BeginExecution(
                new QueryObservationDetails(
                    "TestContext",
                    "TestView",
                    false));

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
        var observability = CreateObservability();

        using var observation =
            observability.BeginExecution(
                new QueryObservationDetails(
                    "TestContext",
                    null,
                    true));

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                observation.ExecutionFailed(null!));

        Assert.Equal(
            "exception",
            exception.ParamName);
    }

    private static QueryableObservability CreateObservability()
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

        return new QueryableObservability(
            correlationAccessor.Object,
            Mock.Of<ILogger<QueryableObservability>>());
    }
}

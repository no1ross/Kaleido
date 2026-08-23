using Kaleido.Observability;

namespace Kaleido.UnitTests;

public sealed class KaleidoCorrelationContextAccessorTests
{
    [Fact]
    public void Current_BeforeInitialization_ReturnsDefaultContext()
    {
        var accessor =
            new KaleidoCorrelationContextAccessor();

        var current = accessor.Current;

        Assert.NotNull(current);
        Assert.Null(current.RequestId);
        Assert.Null(current.ProcessId);
        Assert.Null(current.ParticipantId);
        Assert.Null(current.ParticipantInstanceId);
        Assert.Null(current.OrchestratorId);
        Assert.Null(current.OrchestratorInstanceId);
    }

    [Fact]
    public void Initialize_WhenContextIsNull_Throws()
    {
        var accessor =
            new KaleidoCorrelationContextAccessor();

        Assert.Throws<ArgumentNullException>(() =>
            accessor.Initialize(null!));
    }

    [Fact]
    public void Initialize_UpdatesCurrentContext()
    {
        var accessor =
            new KaleidoCorrelationContextAccessor();

        var context =
            new KaleidoCorrelationContext
            {
                RequestId = "REQ-001",
                ProcessId = Guid.NewGuid(),
                ParticipantId = "participant-a",
                ParticipantInstanceId = Guid.NewGuid(),
                OrchestratorId = "orchestrator-a",
                OrchestratorInstanceId = Guid.NewGuid()
            };

        accessor.Initialize(context);

        Assert.Same(
            context,
            accessor.Current);
    }
}

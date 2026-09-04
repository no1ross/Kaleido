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
        Assert.Null(current.SourceProcessorName);
        Assert.Null(current.ProcessorInstanceId);

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
                ProcessorInstanceId = Guid.NewGuid(),
                SourceProcessorName = "source-processor"
            };

        accessor.Initialize(context);

        Assert.Same(
            context,
            accessor.Current);
    }
}

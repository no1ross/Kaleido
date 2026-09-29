using Kaleido.Observability;

namespace Kaleido.UnitTests.Observability;

public sealed class KaleidoCorrelationContextAccessorTests
    : SutFixture
{
    private KaleidoCorrelationContextAccessor CreateSut() =>
        new();

    [Fact]
    public void Current_BeforeInitialization_ReturnsDefaultContext()
    {
        var accessor =
            CreateSut();

        var current = accessor.Current;

        Assert.NotNull(current);
        Assert.Equal(string.Empty, current.RequestId);
        Assert.Null(current.ProcessId);
        Assert.Null(current.SourceProcessorName);
        Assert.Null(current.ProcessorInstanceId);

    }

    [Fact]
    public void Initialize_WhenContextIsNull_Throws()
    {
        var accessor =
            CreateSut();

        Assert.Throws<ArgumentNullException>(() =>
            accessor.Initialize(null!));
    }

    [Fact]
    public void Initialize_UpdatesCurrentContext()
    {
        var accessor =
            CreateSut();

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

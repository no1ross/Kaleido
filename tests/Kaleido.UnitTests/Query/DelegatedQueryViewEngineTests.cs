using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class DelegatedQueryViewEngineTests
    : Kaleido.UnitTests.SutFixture
{
    private static DelegatedQueryViewEngine<object, object> CreateSut(
        IQueryEventFactory eventFactory,
        IEventPublisher eventPublisher,
        IKaleidoCorrelationContextAccessor correlationAccessor,
        IQueryableObservability observability,
        IServiceProvider serviceProvider) =>
        new(
            eventFactory,
            eventPublisher,
            correlationAccessor,
            observability,
            serviceProvider);

    [Fact]
    public void Constructor_CreatesInstance()
    {
        var eventFactory = new Mock<IQueryEventFactory>();
        var eventPublisher = new Mock<IEventPublisher>();
        var correlationAccessor = new Mock<IKaleidoCorrelationContextAccessor>();
        var observability = new Mock<IQueryableObservability>();
        var serviceProvider = new Mock<IServiceProvider>();

        var engine = CreateSut(
            eventFactory.Object,
            eventPublisher.Object,
            correlationAccessor.Object,
            observability.Object,
            serviceProvider.Object);

        Assert.NotNull(engine);
    }
}

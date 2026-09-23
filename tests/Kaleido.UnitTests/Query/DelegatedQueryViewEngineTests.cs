using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class DelegatedQueryViewEngineTests
{
    [Fact]
    public void Constructor_CreatesInstance()
    {
        var eventFactory = new Mock<IQueryEventFactory>();
        var eventPublisher = new Mock<IEventPublisher>();
        var correlationAccessor = new Mock<IKaleidoCorrelationContextAccessor>();
        var observability = new Mock<IQueryableObservability>();
        var serviceProvider = new Mock<IServiceProvider>();

        var engine = new DelegatedQueryViewEngine<object, object>(
            eventFactory.Object,
            eventPublisher.Object,
            correlationAccessor.Object,
            observability.Object,
            serviceProvider.Object);

        Assert.NotNull(engine);
    }
}

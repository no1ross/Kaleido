using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;

namespace Kaleido.Queryable.Query.UnitTests;

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
            serviceProvider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DelegatedQueryViewEngine<object, object>>.Instance);

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

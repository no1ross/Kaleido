using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using Kaleido.Observability;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

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

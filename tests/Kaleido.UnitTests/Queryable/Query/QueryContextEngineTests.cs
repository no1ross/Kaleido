using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Runtime;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class QueryContextEngineTests
{
    public sealed class FakeContext { }
    public sealed class FakeView { }

    private static QueryContextEngine<FakeContext, FakeView> CreateSut(
        IQueryContextValidator? validator = null,
        IQueryContextCompiler? compiler = null,
        ICompiledQueryApplier<FakeContext>? applier = null,
        IQueryContextExecutor<FakeView>? executor = null,
        IQueryEventFactory? eventFactory = null,
        IEventPublisher? eventPublisher = null,
        IKaleidoCorrelationContextAccessor? correlationAccessor = null,
        IQueryableObservability? observability = null,
        IServiceProvider? serviceProvider = null)
    {
        return new QueryContextEngine<FakeContext, FakeView>(
            validator ?? Mock.Of<IQueryContextValidator>(),
            compiler ?? Mock.Of<IQueryContextCompiler>(),
            applier ?? Mock.Of<ICompiledQueryApplier<FakeContext>>(),
            executor ?? Mock.Of<IQueryContextExecutor<FakeView>>(),
            eventFactory ?? Mock.Of<IQueryEventFactory>(),
            eventPublisher ?? Mock.Of<IEventPublisher>(),
            correlationAccessor ?? Mock.Of<IKaleidoCorrelationContextAccessor>(),
            observability ?? Mock.Of<IQueryableObservability>(),
            serviceProvider ?? Mock.Of<IServiceProvider>());
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        var sut = CreateSut();
        Assert.NotNull(sut);
    }
}

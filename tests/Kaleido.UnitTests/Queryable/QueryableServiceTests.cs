using Kaleido.Exceptions;
using Kaleido.Queryable.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.UnitTests.Queryable;

public sealed class QueryableServiceTests
    : Kaleido.UnitTests.SutFixture
{
    private static QueryableService CreateSut(
        IServiceScopeFactory scopeFactory,
        IDelegatedQueryViewRegistry delegatedViewRegistry,
        IQueryViewRegistry viewRegistry,
        IQueryContextRegistry contextRegistry) =>
        new(
            scopeFactory,
            delegatedViewRegistry,
            viewRegistry,
            contextRegistry);

    [Fact]
    public async Task QueryAsync_WhenViewRegistrationExists_ResolvesTypedEngineAndReturnsResult()
    {
        var request = new QueryRequest();
        var expected = new QueryResult<TestViewContract>(1, 0, 25, [new TestViewContract()]);
        var viewRegistration = CreateViewRegistration();
        var contextRegistration = CreateContextRegistration();

        var engine = new Mock<IQueryContextEngine<TestContext, TestViewContract>>();
        engine.Setup(x => x.ExecuteAsync(request, contextRegistration, viewRegistration, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IQueryContextEngine<TestContext, TestViewContract>))).Returns(engine.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var viewRegistry = new Mock<IQueryViewRegistry>();
        viewRegistry.Setup(x => x.Find(typeof(TestView))).Returns(viewRegistration);

        var contextRegistry = new Mock<IQueryContextRegistry>();
        contextRegistry.Setup(x => x.GetRegistration(typeof(TestContext))).Returns(contextRegistration);

        var service = CreateSut(scopeFactory.Object, Mock.Of<IDelegatedQueryViewRegistry>(), viewRegistry.Object, contextRegistry.Object);

        var result = await service.QueryAsync<TestView, TestViewContract>(request);

        Assert.Same(expected, result);
        engine.Verify(x => x.ExecuteAsync(request, contextRegistration, viewRegistration, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WhenNoViewRegistration_UsesDirectQueryPath()
    {
        var request = new QueryRequest();
        var expected = new QueryResult<TestContext>(1, 0, 25, [new TestContext()]);
        var contextRegistration = CreateContextRegistration();

        var engine = new Mock<IQueryContextEngine<TestContext, TestContext>>();
        engine.Setup(x => x.ExecuteAsync(request, contextRegistration, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IQueryContextEngine<TestContext, TestContext>))).Returns(engine.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var viewRegistry = new Mock<IQueryViewRegistry>();
        viewRegistry.Setup(x => x.Find(typeof(TestContext))).Returns((QueryViewRegistration?)null);

        var contextRegistry = new Mock<IQueryContextRegistry>();
        contextRegistry.Setup(x => x.GetRegistration(typeof(TestContext))).Returns(contextRegistration);

        var service = CreateSut(scopeFactory.Object, Mock.Of<IDelegatedQueryViewRegistry>(), viewRegistry.Object, contextRegistry.Object);

        var result = await service.QueryAsync<TestContext, TestContext>(request);

        Assert.Same(expected, result);
        engine.Verify(x => x.ExecuteAsync(request, contextRegistration, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WhenViewReturnTypeDoesNotMatch_Throws()
    {
        var request = new QueryRequest();
        var viewRegistration = CreateViewRegistration() with { ViewType = typeof(AnotherViewContract) };

        var service = CreateSut(
            Mock.Of<IServiceScopeFactory>(),
            EmptyDelegatedViewRegistry(),
            MockViewRegistry(typeof(TestView), viewRegistration),
            Mock.Of<IQueryContextRegistry>());

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            service.QueryAsync<TestView, TestViewContract>(request));

        Assert.Contains("returns", exception.Message);
    }

    [Fact]
    public async Task QueryAsync_WhenDirectQueryNotAllowed_Throws()
    {
        var request = new QueryRequest();
        var registration = CreateContextRegistration() with { Metadata = CreateContextRegistration().Metadata with { Kind = QueryContextKind.Local } };

        var service = CreateSut(
            Mock.Of<IServiceScopeFactory>(),
            EmptyDelegatedViewRegistry(),
            MockViewRegistry(typeof(TestContext), null),
            MockContextRegistry(registration));

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            service.QueryAsync<TestContext, TestContext>(request));

        Assert.Contains("does not allow direct query", exception.Message);
    }

    [Fact]
    public async Task QueryAsync_WhenDelegatedViewRegistrationExists_UsesDelegatedViewEngine()
    {
        var request = new QueryRequest<EmptyQueryViewParameters>(new EmptyQueryViewParameters(), null);
        var expected = new QueryResult<TestViewContract>(40, 0, 25, [new TestViewContract()]);
        var delegatedRegistration = CreateDelegatedViewRegistration();

        var engine = new Mock<IDelegatedQueryViewEngine<TestContext, TestViewContract>>();
        engine.Setup(x => x.ExecuteAsync(request, delegatedRegistration, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IDelegatedQueryViewEngine<TestContext, TestViewContract>))).Returns(engine.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var delegatedViewRegistry = new Mock<IDelegatedQueryViewRegistry>();
        delegatedViewRegistry.Setup(x => x.Find(typeof(TestView))).Returns(delegatedRegistration);

        var viewRegistry = new Mock<IQueryViewRegistry>();
        viewRegistry.Setup(x => x.Find(typeof(TestView))).Returns((QueryViewRegistration?)null);

        var service = CreateSut(scopeFactory.Object, delegatedViewRegistry.Object, viewRegistry.Object, Mock.Of<IQueryContextRegistry>());

        var result = await service.QueryAsync<TestView, TestViewContract>(request);

        Assert.Same(expected, result);
        engine.Verify(x => x.ExecuteAsync(request, delegatedRegistration, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WhenDelegatedAndLocalViewBothExist_PrefersDelegated()
    {
        var request = new QueryRequest<EmptyQueryViewParameters>(new EmptyQueryViewParameters(), null);
        var expected = new QueryResult<TestViewContract>(1, 0, 25, [new TestViewContract()]);
        var delegatedRegistration = CreateDelegatedViewRegistration();
        var viewRegistration = CreateViewRegistration();

        var delegatedEngine = new Mock<IDelegatedQueryViewEngine<TestContext, TestViewContract>>();
        delegatedEngine.Setup(x => x.ExecuteAsync(request, delegatedRegistration, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var localEngine = new Mock<IQueryContextEngine<TestContext, TestViewContract>>();

        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IDelegatedQueryViewEngine<TestContext, TestViewContract>))).Returns(delegatedEngine.Object);
        provider.Setup(x => x.GetService(typeof(IQueryContextEngine<TestContext, TestViewContract>))).Returns(localEngine.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var delegatedViewRegistry = new Mock<IDelegatedQueryViewRegistry>();
        delegatedViewRegistry.Setup(x => x.Find(typeof(TestView))).Returns(delegatedRegistration);

        var viewRegistry = new Mock<IQueryViewRegistry>();
        viewRegistry.Setup(x => x.Find(typeof(TestView))).Returns(viewRegistration);

        var service = CreateSut(scopeFactory.Object, delegatedViewRegistry.Object, viewRegistry.Object, Mock.Of<IQueryContextRegistry>());

        var result = await service.QueryAsync<TestView, TestViewContract>(request);

        Assert.Same(expected, result);
        localEngine.Verify(x => x.ExecuteAsync(
            It.IsAny<IQueryRequest>(),
            It.IsAny<QueryContextRegistration>(),
            It.IsAny<QueryViewRegistration>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueryAsync_WhenLocalViewAndDirectContextBothExist_PrefersLocalView()
    {
        var request = new QueryRequest();
        var expected = new QueryResult<TestViewContract>(1, 0, 25, [new TestViewContract()]);
        var viewRegistration = CreateViewRegistration();
        var contextRegistration = CreateContextRegistration();

        var viewEngine = new Mock<IQueryContextEngine<TestContext, TestViewContract>>();
        viewEngine.Setup(x => x.ExecuteAsync(request, contextRegistration, viewRegistration, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IQueryContextEngine<TestContext, TestViewContract>))).Returns(viewEngine.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var viewRegistry = new Mock<IQueryViewRegistry>();
        viewRegistry.Setup(x => x.Find(typeof(TestView))).Returns(viewRegistration);

        var contextRegistry = new Mock<IQueryContextRegistry>();
        contextRegistry.Setup(x => x.GetRegistration(typeof(TestContext))).Returns(contextRegistration);

        var service = CreateSut(scopeFactory.Object, EmptyDelegatedViewRegistry(), viewRegistry.Object, contextRegistry.Object);

        var result = await service.QueryAsync<TestView, TestViewContract>(request);

        Assert.Same(expected, result);
        viewEngine.Verify(x => x.ExecuteAsync(request, contextRegistration, viewRegistration, It.IsAny<CancellationToken>()), Times.Once);
        viewEngine.Verify(x => x.ExecuteAsync(
            It.IsAny<IQueryRequest>(),
            It.IsAny<QueryContextRegistration>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // AI-008: exhaustive lane-order proof — when all three lanes could serve
    // the same view, delegated wins: the delegated engine is the only engine
    // invoked and the context registry is never consulted for the local/direct
    // paths. Registry lookups themselves are lookups, not dispatch — pinned
    // dispatch order is delegated > local view > direct context.
    [Fact]
    public async Task QueryAsync_WhenAllThreeLanesExist_DelegatedWinsAndOthersAreUntouched()
    {
        var request = new QueryRequest<EmptyQueryViewParameters>(new EmptyQueryViewParameters(), null);
        var expected = new QueryResult<TestViewContract>(1, 0, 25, [new TestViewContract()]);
        var delegatedRegistration = CreateDelegatedViewRegistration();
        var viewRegistration = CreateViewRegistration();
        var contextRegistration = CreateContextRegistration();

        var delegatedEngine = new Mock<IDelegatedQueryViewEngine<TestContext, TestViewContract>>();
        delegatedEngine.Setup(x => x.ExecuteAsync(request, delegatedRegistration, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var localEngine = new Mock<IQueryContextEngine<TestContext, TestViewContract>>(MockBehavior.Strict);

        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IDelegatedQueryViewEngine<TestContext, TestViewContract>))).Returns(delegatedEngine.Object);
        provider.Setup(x => x.GetService(typeof(IQueryContextEngine<TestContext, TestViewContract>))).Returns(localEngine.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var delegatedViewRegistry = new Mock<IDelegatedQueryViewRegistry>();
        delegatedViewRegistry.Setup(x => x.Find(typeof(TestView))).Returns(delegatedRegistration);

        var viewRegistry = new Mock<IQueryViewRegistry>();
        viewRegistry.Setup(x => x.Find(typeof(TestView))).Returns(viewRegistration);

        var contextRegistry = new Mock<IQueryContextRegistry>(MockBehavior.Strict);

        var service = CreateSut(scopeFactory.Object, delegatedViewRegistry.Object, viewRegistry.Object, contextRegistry.Object);

        var result = await service.QueryAsync<TestView, TestViewContract>(request);

        Assert.Same(expected, result);
        delegatedEngine.Verify(x => x.ExecuteAsync(request, delegatedRegistration, It.IsAny<CancellationToken>()), Times.Once);
        contextRegistry.VerifyNoOtherCalls();
    }

    private static IDelegatedQueryViewRegistry EmptyDelegatedViewRegistry()
    {
        var registry = new Mock<IDelegatedQueryViewRegistry>();
        registry.Setup(x => x.Find(It.IsAny<Type>())).Returns((DelegatedQueryViewRegistration?)null);
        return registry.Object;
    }

    private static IQueryViewRegistry MockViewRegistry(Type lookupType, QueryViewRegistration? registration)
    {
        var registry = new Mock<IQueryViewRegistry>();
        registry.Setup(x => x.Find(lookupType)).Returns(registration);
        return registry.Object;
    }

    private static IQueryContextRegistry MockContextRegistry(QueryContextRegistration registration)
    {
        var registry = new Mock<IQueryContextRegistry>();
        registry.Setup(x => x.GetRegistration(typeof(TestContext))).Returns(registration);
        return registry.Object;
    }

    private static QueryContextRegistration CreateContextRegistration() =>
        new(
            typeof(TestContext),
            typeof(object),
            new QueryContextMetadata("test-context", "Test Context", "Test Context", "1.0.0", "Unit Test", QueryContextKind.Direct, null, []));

    private static DelegatedQueryViewRegistration CreateDelegatedViewRegistration() =>
        new(
            typeof(TestView),
            typeof(TestViewContract),
            typeof(EmptyQueryViewParameters),
            typeof(TestContext),
            new QueryContextMetadata("test-context", "Test Context", "Test Context", "1.0.0", "Unit Test", QueryContextKind.Delegated, new PageableMetadata(25, 250), []),
            new QueryViewMetadata("test-view", "1.0.0", "Test View", "Test View", null, [], []));

    private static QueryViewRegistration CreateViewRegistration() =>
        new(
            typeof(TestView),
            typeof(TestViewContract),
            typeof(EmptyQueryViewParameters),
            typeof(TestContext),
            new QueryViewMetadata("test-view", "1.0.0", "Test View", "Test View", null, [], []));

    public sealed class TestContext
    {
    }

    public sealed class TestView
    {
    }

    public sealed class TestViewContract
    {
    }

    public sealed class AnotherViewContract
    {
    }
}

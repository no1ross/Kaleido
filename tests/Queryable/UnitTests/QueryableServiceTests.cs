using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableServiceTests
{
    [Fact]
    public void Constructor_WhenScopeFactoryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new QueryableService(null!, Mock.Of<IQueryViewRegistry>(), Mock.Of<IQueryContextRegistry>()));
    }

    [Fact]
    public void Constructor_WhenViewRegistryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new QueryableService(Mock.Of<IServiceScopeFactory>(), null!, Mock.Of<IQueryContextRegistry>()));
    }

    [Fact]
    public void Constructor_WhenContextRegistryIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new QueryableService(Mock.Of<IServiceScopeFactory>(), Mock.Of<IQueryViewRegistry>(), null!));
    }

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

        var service = new QueryableService(scopeFactory.Object, viewRegistry.Object, contextRegistry.Object);

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

        var service = new QueryableService(scopeFactory.Object, viewRegistry.Object, contextRegistry.Object);

        var result = await service.QueryAsync<TestContext, TestContext>(request);

        Assert.Same(expected, result);
        engine.Verify(x => x.ExecuteAsync(request, contextRegistration, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WhenViewReturnTypeDoesNotMatch_Throws()
    {
        var request = new QueryRequest();
        var viewRegistration = CreateViewRegistration() with { ViewType = typeof(AnotherViewContract) };

        var service = new QueryableService(
            Mock.Of<IServiceScopeFactory>(),
            MockViewRegistry(typeof(TestView), viewRegistration),
            Mock.Of<IQueryContextRegistry>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QueryAsync<TestView, TestViewContract>(request));

        Assert.Contains("returns", exception.Message);
    }

    [Fact]
    public async Task QueryAsync_WhenDirectQueryNotAllowed_Throws()
    {
        var request = new QueryRequest();
        var registration = CreateContextRegistration() with { Metadata = CreateContextRegistration().Metadata with { AllowDirectQuery = false } };

        var service = new QueryableService(
            Mock.Of<IServiceScopeFactory>(),
            MockViewRegistry(typeof(TestContext), null),
            MockContextRegistry(registration));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QueryAsync<TestContext, TestContext>(request));

        Assert.Contains("does not allow direct query", exception.Message);
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
            new QueryContextMetadata("test-context", "Test Context", "Test Context", "1.0.0", "Unit Test", true, null, []));

    private static QueryViewRegistration CreateViewRegistration() =>
        new(
            typeof(TestView),
            typeof(TestViewContract),
            typeof(EmptyQueryViewParameters),
            typeof(TestContext),
            new QueryViewMetadata("test-view", "1.0.0", "Test View", "Test View", null, []));

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

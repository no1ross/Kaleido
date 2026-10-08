using Kaleido.Exceptions;
using Kaleido.Queryable.Registry;
using Kaleido.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableServiceTests
    : Kaleido.UnitTests.SutFixture
{
    private static QueryableService CreateSut(
        IServiceScopeFactory scopeFactory,
        IQueryViewRegistry viewRegistry,
        IQuerySourceRegistry sourceRegistry) =>
        new(
            scopeFactory,
            viewRegistry,
            sourceRegistry);

    [Fact]
    public async Task QueryAsync_WhenTypeIsView_UsesLocalEngineWithViewAndItsSource()
    {
        var request = new QueryRequest();
        var expected = new QueryResult<TestViewRecord>(1, 0, 25, [new TestViewRecord()]);
        var source = CreateSourceRegistration(typeof(TestSource), QuerySourceKind.Local, typeof(TestContext));
        var view = CreateViewRegistration();

        var engine = new Mock<IQueryContextEngine<TestContext, TestViewRecord>>();
        engine.Setup(x => x.ExecuteAsync(request, source, view, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var sourceRegistry = new Mock<IQuerySourceRegistry>();
        sourceRegistry.Setup(x => x.GetRegistration(typeof(TestSource))).Returns(source);

        var service = CreateSut(
            ScopeFactoryReturning(typeof(IQueryContextEngine<TestContext, TestViewRecord>), engine.Object),
            ViewRegistryWith(view),
            sourceRegistry.Object);

        var result = await service.QueryAsync<TestView, TestViewRecord>(request);

        Assert.Same(expected, result);
        engine.Verify(x => x.ExecuteAsync(request, source, view, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WhenTypeIsLocalSource_UsesDirectQuery()
    {
        var request = new QueryRequest();
        var expected = new QueryResult<TestContext>(1, 0, 25, [new TestContext()]);
        var source = CreateSourceRegistration(typeof(TestSource), QuerySourceKind.Local, typeof(TestContext));

        var engine = new Mock<IQueryContextEngine<TestContext, TestContext>>();
        engine.Setup(x => x.ExecuteAsync(request, source, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var service = CreateSut(
            ScopeFactoryReturning(typeof(IQueryContextEngine<TestContext, TestContext>), engine.Object),
            ViewRegistryWith(null),
            SourceRegistryWith(typeof(TestSource), source));

        var result = await service.QueryAsync<TestSource, TestContext>(request);

        Assert.Same(expected, result);
        engine.Verify(x => x.ExecuteAsync(request, source, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WhenTypeIsDelegatedSource_UsesDelegatedEngine()
    {
        var request = new QueryRequest();
        var expected = new QueryResult<TestViewRecord>(40, 0, 25, [new TestViewRecord()]);
        var source = CreateSourceRegistration(typeof(TestDelegatedSource), QuerySourceKind.Delegated, typeof(TestViewRecord));

        var engine = new Mock<IDelegatedQuerySourceEngine<TestContext, TestViewRecord>>();
        engine.Setup(x => x.ExecuteAsync(request, source, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var service = CreateSut(
            ScopeFactoryReturning(typeof(IDelegatedQuerySourceEngine<TestContext, TestViewRecord>), engine.Object),
            ViewRegistryWith(null),
            SourceRegistryWith(typeof(TestDelegatedSource), source));

        var result = await service.QueryAsync<TestDelegatedSource, TestViewRecord>(request);

        Assert.Same(expected, result);
        engine.Verify(x => x.ExecuteAsync(request, source, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WhenViewReturnTypeDoesNotMatch_Throws()
    {
        var view = CreateViewRegistration() with { ViewType = typeof(AnotherRecord) };

        var service = CreateSut(
            ScopeFactoryReturning(typeof(object), new object()),
            ViewRegistryWith(view),
            Mock.Of<IQuerySourceRegistry>());

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            service.QueryAsync<TestView, TestViewRecord>(new QueryRequest()));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
    }

    [Fact]
    public async Task QueryAsync_WhenSourceResultTypeDoesNotMatch_Throws()
    {
        var source = CreateSourceRegistration(typeof(TestSource), QuerySourceKind.Local, typeof(TestContext));

        var service = CreateSut(
            ScopeFactoryReturning(typeof(object), new object()),
            ViewRegistryWith(null),
            SourceRegistryWith(typeof(TestSource), source));

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            service.QueryAsync<TestSource, TestViewRecord>(new QueryRequest()));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
    }

    [Fact]
    public async Task QueryAsync_WhenTypeIsNotRegistered_PropagatesMissingRegistration()
    {
        var sourceRegistry = new Mock<IQuerySourceRegistry>();
        sourceRegistry
            .Setup(x => x.GetRegistration(typeof(AnotherRecord)))
            .Throws(new KaleidoFrameworkException(FrameworkErrorCodes.MissingRegistration, "not registered"));

        var service = CreateSut(
            ScopeFactoryReturning(typeof(object), new object()),
            ViewRegistryWith(null),
            sourceRegistry.Object);

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            service.QueryAsync<AnotherRecord, AnotherRecord>(new QueryRequest()));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
    }

    private static IServiceScopeFactory ScopeFactoryReturning(Type serviceType, object service)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(serviceType)).Returns(service);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return scopeFactory.Object;
    }

    private static IQueryViewRegistry ViewRegistryWith(QueryViewRegistration? registration)
    {
        var registry = new Mock<IQueryViewRegistry>();
        registry.Setup(x => x.Find(It.IsAny<Type>())).Returns(null);

        if (registration is not null)
        {
            registry.Setup(x => x.Find(registration.QueryViewType)).Returns(registration);
        }

        return registry.Object;
    }

    private static IQuerySourceRegistry SourceRegistryWith(Type sourceType, QuerySourceRegistration registration)
    {
        var registry = new Mock<IQuerySourceRegistry>();
        registry.Setup(x => x.GetRegistration(sourceType)).Returns(registration);
        return registry.Object;
    }

    private static QuerySourceRegistration CreateSourceRegistration(
        Type sourceType,
        QuerySourceKind kind,
        Type resultType) =>
        new(
            sourceType,
            typeof(TestContext),
            resultType,
            typeof(EmptyQueryViewParameters),
            new QuerySourceMetadata(sourceType.Name, "Test", "Test", "1.0.0", "Unit Test", kind, null, [], [], [], AuthorizationMetadata.Unspecified));

    private static QueryViewRegistration CreateViewRegistration() =>
        new(
            typeof(TestView),
            typeof(TestViewRecord),
            typeof(EmptyQueryViewParameters),
            typeof(TestSource),
            typeof(TestContext),
            new QueryViewMetadata(nameof(TestView), "1.0.0", "Test View", "Test View", null, [], []));

    public sealed class TestContext : IQueryContext
    {
    }

    public sealed class TestSource
    {
    }

    public sealed class TestDelegatedSource
    {
    }

    public sealed class TestView
    {
    }

    public sealed class TestViewRecord
    {
    }

    public sealed class AnotherRecord
    {
    }
}

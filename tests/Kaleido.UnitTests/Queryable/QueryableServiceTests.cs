using System.Security.Claims;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Queryable.Registry;
using Kaleido.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableServiceTests
    : Kaleido.UnitTests.SutFixture
{
    // The request's correlation, as the HTTP transport leaves it for the query.
    private static readonly KaleidoCorrelationContext RequestCorrelation = new()
    {
        RequestId = "req-413",
        ProcessId = Guid.NewGuid(),
        CallerName = "ann",
        CallerRoles = ["clerk"],
    };

    // What the query's own scope was initialized with.
    private readonly List<KaleidoCorrelationContext> _initialized = [];

    // The inbound caller the query's own scope was given (principal, credential), one entry per Set.
    private readonly List<(ClaimsPrincipal? Principal, string? Credential)> _inboundSet = [];

    private static QueryableService CreateSut(
        IServiceScopeFactory scopeFactory,
        IQueryViewRegistry viewRegistry,
        IQuerySourceRegistry sourceRegistry,
        IKaleidoInboundCallerAccessor? inboundCaller = null)
    {
        var accessor = new Mock<IKaleidoCorrelationContextAccessor>();
        accessor.SetupGet(x => x.Current).Returns(RequestCorrelation);

        return new(
            scopeFactory,
            accessor.Object,
            inboundCaller ?? InboundCaller(isInboundRequest: false, principal: null, credential: null),
            viewRegistry,
            sourceRegistry);
    }

    private static IKaleidoInboundCallerAccessor InboundCaller(bool isInboundRequest, ClaimsPrincipal? principal, string? credential)
    {
        var accessor = new Mock<IKaleidoInboundCallerAccessor>();
        accessor.SetupGet(x => x.IsInboundRequest).Returns(isInboundRequest);
        accessor.SetupGet(x => x.Principal).Returns(principal);
        accessor.SetupGet(x => x.Credential).Returns(credential);
        return accessor.Object;
    }

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

        var result = await service.QueryAsync<TestView, TestViewRecord>(request, Xunit.TestContext.Current.CancellationToken);

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

        var result = await service.QueryAsync<TestSource, TestContext>(request, Xunit.TestContext.Current.CancellationToken);

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

        var result = await service.QueryAsync<TestDelegatedSource, TestViewRecord>(request, Xunit.TestContext.Current.CancellationToken);

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
            service.QueryAsync<TestView, TestViewRecord>(new QueryRequest(), Xunit.TestContext.Current.CancellationToken));

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
            service.QueryAsync<TestSource, TestViewRecord>(new QueryRequest(), Xunit.TestContext.Current.CancellationToken));

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
            service.QueryAsync<AnotherRecord, AnotherRecord>(new QueryRequest(), Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
    }

    [Fact]
    public async Task QueryAsync_WhenTheEngineReturnsNoTask_ThrowsTypeMismatch()
    {
        var request = new QueryRequest();
        var source = CreateSourceRegistration(typeof(TestSource), QuerySourceKind.Local, typeof(TestContext));
        var engine = new Mock<IQueryContextEngine<TestContext, TestContext>>();
        engine.Setup(x => x.ExecuteAsync(request, source, It.IsAny<CancellationToken>())).Returns((Task<QueryResult<TestContext>>)null!);

        var service = CreateSut(
            ScopeFactoryReturning(typeof(IQueryContextEngine<TestContext, TestContext>), engine.Object),
            ViewRegistryWith(null),
            SourceRegistryWith(typeof(TestSource), source));

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            service.QueryAsync<TestSource, TestContext>(request, Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
    }

    [Fact]
    public async Task QueryAsync_WhenRequestIsNull_Throws()
    {
        var service = CreateSut(Mock.Of<IServiceScopeFactory>(), ViewRegistryWith(null), Mock.Of<IQuerySourceRegistry>());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => service.QueryAsync<TestSource, TestContext>(null!, Xunit.TestContext.Current.CancellationToken));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task QueryAsync_WhenTheEngineFails_PropagatesTheFailure()
    {
        var request = new QueryRequest();
        var source = CreateSourceRegistration(typeof(TestSource), QuerySourceKind.Local, typeof(TestContext));
        var engine = new Mock<IQueryContextEngine<TestContext, TestContext>>();
        engine.Setup(x => x.ExecuteAsync(request, source, It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        var service = CreateSut(
            ScopeFactoryReturning(typeof(IQueryContextEngine<TestContext, TestContext>), engine.Object),
            ViewRegistryWith(null),
            SourceRegistryWith(typeof(TestSource), source));

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.QueryAsync<TestSource, TestContext>(request, Xunit.TestContext.Current.CancellationToken));
    }

    private IServiceScopeFactory ScopeFactoryReturning(Type serviceType, object service)
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        initializer.Setup(x => x.Initialize(It.IsAny<KaleidoCorrelationContext>())).Callback<KaleidoCorrelationContext>(_initialized.Add);

        var inbound = new Mock<IKaleidoInboundCallerAccessor>();
        inbound.Setup(x => x.Set(It.IsAny<ClaimsPrincipal?>(), It.IsAny<string?>()))
            .Callback<ClaimsPrincipal?, string?>((principal, credential) => _inboundSet.Add((principal, credential)));

        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(serviceType)).Returns(service);
        provider.Setup(x => x.GetService(typeof(IKaleidoCorrelationContextInitializer))).Returns(initializer.Object);
        provider.Setup(x => x.GetService(typeof(IKaleidoInboundCallerAccessor))).Returns(inbound.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(provider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return scopeFactory.Object;
    }

    // #413: sources and views run in the query's own scope; it starts from the request's correlation
    // (caller, request id, process id), never an empty one - whichever lane runs the query.
    [Fact]
    public async Task QueryAsync_View_RunsWithTheRequestsCorrelation()
    {
        var source = CreateSourceRegistration(typeof(TestSource), QuerySourceKind.Local, typeof(TestContext));
        var view = CreateViewRegistration();
        var engine = new Mock<IQueryContextEngine<TestContext, TestViewRecord>>();
        engine.Setup(x => x.ExecuteAsync(It.IsAny<IQueryRequest>(), source, view, It.IsAny<CancellationToken>())).ReturnsAsync(new QueryResult<TestViewRecord>(0, 0, 25, []));
        var service = CreateSut(ScopeFactoryReturning(typeof(IQueryContextEngine<TestContext, TestViewRecord>), engine.Object), ViewRegistryWith(view), SourceRegistryWith(typeof(TestSource), source));

        await service.QueryAsync<TestView, TestViewRecord>(new QueryRequest(), Xunit.TestContext.Current.CancellationToken);

        Assert.Same(RequestCorrelation, Assert.Single(_initialized));
    }

    [Fact]
    public async Task QueryAsync_LocalSource_RunsWithTheRequestsCorrelation()
    {
        var source = CreateSourceRegistration(typeof(TestSource), QuerySourceKind.Local, typeof(TestContext));
        var engine = new Mock<IQueryContextEngine<TestContext, TestContext>>();
        engine.Setup(x => x.ExecuteAsync(It.IsAny<IQueryRequest>(), source, It.IsAny<CancellationToken>())).ReturnsAsync(new QueryResult<TestContext>(0, 0, 25, []));
        var service = CreateSut(ScopeFactoryReturning(typeof(IQueryContextEngine<TestContext, TestContext>), engine.Object), ViewRegistryWith(null), SourceRegistryWith(typeof(TestSource), source));

        await service.QueryAsync<TestSource, TestContext>(new QueryRequest(), Xunit.TestContext.Current.CancellationToken);

        Assert.Same(RequestCorrelation, Assert.Single(_initialized));
    }

    [Fact]
    public async Task QueryAsync_DelegatedSource_RunsWithTheRequestsCorrelation()
    {
        var source = CreateSourceRegistration(typeof(TestDelegatedSource), QuerySourceKind.Delegated, typeof(TestViewRecord));
        var engine = new Mock<IDelegatedQuerySourceEngine<TestContext, TestViewRecord>>();
        engine.Setup(x => x.ExecuteAsync(It.IsAny<IQueryRequest>(), source, It.IsAny<CancellationToken>())).ReturnsAsync(new QueryResult<TestViewRecord>(0, 0, 25, []));
        var service = CreateSut(ScopeFactoryReturning(typeof(IDelegatedQuerySourceEngine<TestContext, TestViewRecord>), engine.Object), ViewRegistryWith(null), SourceRegistryWith(typeof(TestDelegatedSource), source));

        await service.QueryAsync<TestDelegatedSource, TestViewRecord>(new QueryRequest(), Xunit.TestContext.Current.CancellationToken);

        Assert.Same(RequestCorrelation, Assert.Single(_initialized));
    }

    // #428: a source's outbound calls are made for the request's caller (ADR 0022) only if the query's
    // scope knows that caller, as it knows the request's correlation.
    [Fact]
    public async Task QueryAsync_CarriesTheRequestsInboundCallerIntoTheQueryScope()
    {
        var bob = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "bob")], "test"));
        var source = CreateSourceRegistration(typeof(TestDelegatedSource), QuerySourceKind.Delegated, typeof(TestViewRecord));
        var engine = new Mock<IDelegatedQuerySourceEngine<TestContext, TestViewRecord>>();
        engine.Setup(x => x.ExecuteAsync(It.IsAny<IQueryRequest>(), source, It.IsAny<CancellationToken>())).ReturnsAsync(new QueryResult<TestViewRecord>(0, 0, 25, []));
        var service = CreateSut(
            ScopeFactoryReturning(typeof(IDelegatedQuerySourceEngine<TestContext, TestViewRecord>), engine.Object),
            ViewRegistryWith(null),
            SourceRegistryWith(typeof(TestDelegatedSource), source),
            InboundCaller(isInboundRequest: true, bob, "Bearer bob"));

        await service.QueryAsync<TestDelegatedSource, TestViewRecord>(new QueryRequest(), Xunit.TestContext.Current.CancellationToken);

        var (principal, credential) = Assert.Single(_inboundSet);
        Assert.Same(bob, principal);
        Assert.Equal("Bearer bob", credential);
    }

    [Fact]
    public async Task QueryAsync_AnAnonymousInboundCaller_StaysAnonymousInTheQueryScope()
    {
        var source = CreateSourceRegistration(typeof(TestSource), QuerySourceKind.Local, typeof(TestContext));
        var engine = new Mock<IQueryContextEngine<TestContext, TestContext>>();
        engine.Setup(x => x.ExecuteAsync(It.IsAny<IQueryRequest>(), source, It.IsAny<CancellationToken>())).ReturnsAsync(new QueryResult<TestContext>(0, 0, 25, []));
        var service = CreateSut(
            ScopeFactoryReturning(typeof(IQueryContextEngine<TestContext, TestContext>), engine.Object),
            ViewRegistryWith(null),
            SourceRegistryWith(typeof(TestSource), source),
            InboundCaller(isInboundRequest: true, principal: null, credential: null));

        await service.QueryAsync<TestSource, TestContext>(new QueryRequest(), Xunit.TestContext.Current.CancellationToken);

        var (principal, credential) = Assert.Single(_inboundSet);
        Assert.Null(principal);
        Assert.Null(credential);
    }

    // Outside a request (no inbound caller), the query's calls stay the service's own.
    [Fact]
    public async Task QueryAsync_OutsideARequest_SetsNoInboundCallerInTheQueryScope()
    {
        var source = CreateSourceRegistration(typeof(TestSource), QuerySourceKind.Local, typeof(TestContext));
        var engine = new Mock<IQueryContextEngine<TestContext, TestContext>>();
        engine.Setup(x => x.ExecuteAsync(It.IsAny<IQueryRequest>(), source, It.IsAny<CancellationToken>())).ReturnsAsync(new QueryResult<TestContext>(0, 0, 25, []));
        var service = CreateSut(
            ScopeFactoryReturning(typeof(IQueryContextEngine<TestContext, TestContext>), engine.Object),
            ViewRegistryWith(null),
            SourceRegistryWith(typeof(TestSource), source));

        await service.QueryAsync<TestSource, TestContext>(new QueryRequest(), Xunit.TestContext.Current.CancellationToken);

        Assert.Empty(_inboundSet);
    }

    private static IQueryViewRegistry ViewRegistryWith(QueryViewRegistration? registration)
    {
        // Loose mock: Find returns null (not a view) for any type not set up below.
        var registry = new Mock<IQueryViewRegistry>();

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

using Kaleido.Registry;
using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Runtime;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryContextEngineTests
    : Kaleido.UnitTests.SutFixture
{
    public sealed class FakeContext : IQueryContext
    {
        public string Code { get; init; } = string.Empty;
    }

    public sealed class FakeView
    {
        public string Label { get; init; } = string.Empty;
    }

    // Public (Moq proxies generic interfaces over these types) and therefore discoverable by
    // fixtures that scan this assembly, so they carry valid metadata.
    [QuerySource(Version = "1.0.0", DisplayName = "Fake", Description = "Engine test source.")]
    public sealed class FakeSource : IQuerySource<FakeContext>
    {
        public QueryExecutionContext? ReceivedContext { get; private set; }

        public IQueryable<FakeContext> CreateQuery(QueryExecutionContext executionContext)
        {
            ReceivedContext = executionContext;
            return new[] { new FakeContext { Code = "A" }, new FakeContext { Code = "B" } }.AsQueryable();
        }
    }

    [QuerySource(Version = "1.0.0", DisplayName = "Fake async", Description = "Engine test async source.")]
    public sealed class FakeAsyncSource : IQuerySourceAsync<FakeContext>
    {
        public Task<IQueryable<FakeContext>> CreateQueryAsync(
            QueryExecutionContext executionContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new[] { new FakeContext { Code = "Z" } }.AsQueryable());
    }

    [QueryView(Version = "1.0.0", DisplayName = "Labels", Description = "Engine test view.")]
    public sealed class FakeLabelView : IQueryViewSource<FakeSource, FakeContext, FakeView>
    {
        public IQueryable<FakeView> CreateView(IQueryable<FakeContext> query, QueryExecutionContext executionContext) =>
            query.Select(x => new FakeView { Label = "label-" + x.Code });
    }

    private static QueryContextEngine<FakeContext, TView> CreateSut<TView>(
        IQueryContextValidator validator,
        IServiceProvider serviceProvider)
        where TView : class
    {
        var compiler = new Mock<IQueryContextCompiler>();
        compiler
            .Setup(x => x.Compile(It.IsAny<IQueryRequest>(), It.IsAny<QuerySourceMetadata>(), It.IsAny<PageableMetadata?>()))
            .Returns(new CompiledQuery(null, null, [], new CompiledPage(50, 0, false)));

        var applier = new Mock<ICompiledQueryApplier<FakeContext>>();
        applier
            .Setup(x => x.ApplySearch(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<CompiledSearch?>()))
            .Returns((IQueryable<FakeContext> query, CompiledSearch? _) => query);
        applier
            .Setup(x => x.ApplyFilter(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<CompiledFilterExpression?>()))
            .Returns((IQueryable<FakeContext> query, CompiledFilterExpression? _) => query);
        applier
            .Setup(x => x.ApplySort(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<IReadOnlyList<CompiledSort>>()))
            .Returns((IQueryable<FakeContext> query, IReadOnlyList<CompiledSort> _) => query);

        var executor = new Mock<IQueryContextExecutor<TView>>();
        executor
            .Setup(x => x.ApplyPage(It.IsAny<IQueryable<TView>>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((IQueryable<TView> query, int _, int _) => query);
        executor
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<TView>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IQueryable<TView> query, CancellationToken _) => query.ToList());

        var observability = new Mock<IQueryableObservability>();
        observability
            .Setup(x => x.BeginExecution(It.IsAny<QueryObservationDetails>()))
            .Returns(Mock.Of<IQueryExecutionObservation>());

        return new QueryContextEngine<FakeContext, TView>(
            validator,
            compiler.Object,
            applier.Object,
            executor.Object,
            Mock.Of<IQueryEventFactory>(),
            Mock.Of<IEventPublisher>(),
            Mock.Of<IKaleidoCorrelationContextAccessor>(),
            observability.Object,
            serviceProvider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<QueryContextEngine<FakeContext, TView>>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_Direct_ResolvesConcreteSourceAndValidatesWithSourcePaging()
    {
        var source = new FakeSource();
        var registration = CreateSourceRegistration(typeof(FakeSource), new PageableMetadata(25, 100));
        var validator = new Mock<IQueryContextValidator>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(source);

        var request = new QueryRequest();
        var result = await CreateSut<FakeContext>(validator.Object, serviceProvider.Object)
            .ExecuteAsync(request, registration);

        Assert.Equal(["A", "B"], result.Results.Select(x => x.Code));
        Assert.Same(registration.Metadata, source.ReceivedContext?.Metadata);
        validator.Verify(x => x.Validate(request, registration.Metadata, registration.Metadata.Pageable), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Direct_UsesAsyncSource()
    {
        var registration = CreateSourceRegistration(typeof(FakeAsyncSource), pageable: null);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeAsyncSource))).Returns(new FakeAsyncSource());

        var result = await CreateSut<FakeContext>(Mock.Of<IQueryContextValidator>(), serviceProvider.Object)
            .ExecuteAsync(new QueryRequest(), registration);

        Assert.Equal("Z", Assert.Single(result.Results).Code);
    }

    [Fact]
    public async Task ExecuteAsync_View_ProjectsSourceAndValidatesWithViewPaging()
    {
        var sourceRegistration = CreateSourceRegistration(typeof(FakeSource), new PageableMetadata(25, 100));
        var viewPageable = new PageableMetadata(10, 20);
        var viewRegistration =
            new QueryViewRegistration(
                typeof(FakeLabelView),
                typeof(FakeView),
                typeof(EmptyQueryViewParameters),
                typeof(FakeSource),
                typeof(FakeContext),
                new QueryViewMetadata(nameof(FakeLabelView), "1.0.0", "Labels", "Labels.", viewPageable, [], []));

        var validator = new Mock<IQueryContextValidator>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        serviceProvider.Setup(x => x.GetService(typeof(FakeLabelView))).Returns(new FakeLabelView());

        var request = new QueryRequest();
        var result = await CreateSut<FakeView>(validator.Object, serviceProvider.Object)
            .ExecuteAsync(request, sourceRegistration, viewRegistration);

        Assert.Equal(["label-A", "label-B"], result.Results.Select(x => x.Label));
        validator.Verify(x => x.Validate(request, sourceRegistration.Metadata, viewPageable), Times.Once);
    }

    private static QuerySourceRegistration CreateSourceRegistration(
        Type sourceType,
        PageableMetadata? pageable) =>
        new(
            sourceType,
            typeof(FakeContext),
            typeof(FakeContext),
            typeof(EmptyQueryViewParameters),
            new QuerySourceMetadata(
                sourceType.Name,
                "Fake source.",
                "Fake",
                "1.0.0",
                null,
                QuerySourceKind.Local,
                pageable,
                [],
                [],
                [],
                AuthorizationMetadata.Unspecified));
}

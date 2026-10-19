using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Runtime;
using Kaleido.Registry;

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
        IServiceProvider serviceProvider,
        CompiledPage? page = null,
        Mock<IQueryContextExecutor<TView>>? executor = null,
        bool returnsNoRows = false,
        Mock<IQueryExecutionObservation>? observation = null,
        Mock<IKaleidoEventOutbox>? outbox = null)
        where TView : class
    {
        var compiler = new Mock<IQueryContextCompiler>();
        compiler
            .Setup(x => x.Compile(It.IsAny<IQueryRequest>(), It.IsAny<QuerySourceMetadata>(), It.IsAny<PageableMetadata?>()))
            .Returns(new CompiledQuery(null, null, [], page ?? new CompiledPage(50, 0)));

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

        executor ??= new Mock<IQueryContextExecutor<TView>>();
        executor
            .Setup(x => x.ApplyPage(It.IsAny<IQueryable<TView>>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((IQueryable<TView> query, int _, int _) => returnsNoRows ? query.Take(0) : query);
        executor
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<TView>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IQueryable<TView> query, CancellationToken _) => query.ToList());

        var observability = new Mock<IQueryableObservability>();
        observability
            .Setup(x => x.BeginExecution(It.IsAny<QueryObservationDetails>()))
            .Returns(observation?.Object ?? Mock.Of<IQueryExecutionObservation>());

        return new QueryContextEngine<FakeContext, TView>(
            validator,
            compiler.Object,
            applier.Object,
            executor.Object,
            Mock.Of<IQueryEventFactory>(),
            outbox?.Object ?? Mock.Of<IKaleidoEventOutbox>(),
            Mock.Of<IKaleidoCorrelationContextAccessor>(),
            observability.Object,
            serviceProvider);
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
        Assert.Same(request, source.ReceivedContext?.Request);
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

    // A pageable source pages with its default page when the request names none, so a full page is
    // counted: the total is not the page's length (#343).
    [Fact]
    public async Task ExecuteAsync_PageableWithoutAPageInTheRequest_AFullDefaultPage_ReportsTheTrueTotal()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), new PageableMetadata(2, 100));
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        var executor = new Mock<IQueryContextExecutor<FakeContext>>();
        executor.Setup(x => x.CountAsync(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<CancellationToken>())).ReturnsAsync(44);

        var result = await CreateSut(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, new CompiledPage(2, 0), executor)
            .ExecuteAsync(new QueryRequest(), registration);

        Assert.Equal(2, result.Results.Count);
        Assert.Equal(44, result.TotalCount);
    }

    [Fact]
    public async Task ExecuteAsync_PageableWithAPartialPage_ReportsTheReturnedCount_WithoutCounting()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), new PageableMetadata(25, 100));
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        var executor = new Mock<IQueryContextExecutor<FakeContext>>();

        var result = await CreateSut(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, new CompiledPage(25, 0), executor)
            .ExecuteAsync(new QueryRequest(), registration);

        Assert.Equal(2, result.TotalCount);
        executor.Verify(x => x.CountAsync(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The last page: its offset plus its rows is the total, without a count query.
    [Fact]
    public async Task ExecuteAsync_PageableWithAPartialPageAfterTheStart_ReportsOffsetPlusReturned_WithoutCounting()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), new PageableMetadata(25, 100));
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        var executor = new Mock<IQueryContextExecutor<FakeContext>>();

        var result = await CreateSut(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, new CompiledPage(4, 4), executor)
            .ExecuteAsync(new QueryRequest(), registration);

        Assert.Equal(6, result.TotalCount);
        executor.Verify(x => x.CountAsync(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // An offset past the end returns no rows: only a count says how many there are.
    [Fact]
    public async Task ExecuteAsync_PageableWithAnEmptyPagePastTheEnd_ReportsTheTrueTotal()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), new PageableMetadata(25, 100));
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        var executor = new Mock<IQueryContextExecutor<FakeContext>>();
        executor.Setup(x => x.CountAsync(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await CreateSut(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, new CompiledPage(25, 50), executor, returnsNoRows: true)
            .ExecuteAsync(new QueryRequest(), registration);

        Assert.Empty(result.Results);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task ExecuteAsync_NotPageable_ReportsTheReturnedCount_WithoutCounting()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), pageable: null);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        var executor = new Mock<IQueryContextExecutor<FakeContext>>();

        var result = await CreateSut(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, new CompiledPage(2, 0), executor)
            .ExecuteAsync(new QueryRequest(), registration);

        Assert.Equal(2, result.TotalCount);
        executor.Verify(x => x.CountAsync(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Direct_OnSuccess_EnqueuesTheExecutedEventAndRecordsMaterialization()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), pageable: null);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        var observation = new Mock<IQueryExecutionObservation>();
        var outbox = new Mock<IKaleidoEventOutbox>();

        await CreateSut<FakeContext>(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, observation: observation, outbox: outbox)
            .ExecuteAsync(new QueryRequest(), registration);

        observation.Verify(x => x.BeginSource(), Times.Once);
        observation.Verify(x => x.BeginMaterialization(), Times.Once);
        observation.Verify(x => x.Materialized(2, 2, 50, 0), Times.Once);
        outbox.Verify(x => x.EnqueueAsync(It.IsAny<KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Direct_AwaitsAnAsyncSourceThatCompletesLater()
    {
        var registration = CreateSourceRegistration(typeof(FakeAsyncSource), pageable: null);
        var source = new Mock<IQuerySourceAsync<FakeContext>>();
        source
            .Setup(x => x.CreateQueryAsync(It.IsAny<QueryExecutionContext>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                return new[] { new FakeContext { Code = "later" } }.AsQueryable();
            });
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeAsyncSource))).Returns(source.Object);

        var result = await CreateSut<FakeContext>(Mock.Of<IQueryContextValidator>(), serviceProvider.Object)
            .ExecuteAsync(new QueryRequest(), registration);

        Assert.Equal("later", Assert.Single(result.Results).Code);
    }

    [Fact]
    public async Task ExecuteAsync_Direct_WhenValidationFails_RecordsAValidationFailureOnly()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), pageable: null);
        var validator = new Mock<IQueryContextValidator>();
        var failure = new KaleidoValidationException("qry_bad", "bad");
        validator.Setup(x => x.Validate(It.IsAny<IQueryRequest>(), It.IsAny<QuerySourceMetadata>(), It.IsAny<PageableMetadata?>())).Throws(failure);
        var observation = new Mock<IQueryExecutionObservation>();

        var thrown = await Assert.ThrowsAsync<KaleidoValidationException>(
            () => CreateSut<FakeContext>(validator.Object, Mock.Of<IServiceProvider>(), observation: observation)
                .ExecuteAsync(new QueryRequest(), registration));

        Assert.Same(failure, thrown);
        observation.Verify(x => x.ValidationFailed(failure), Times.Once);
        observation.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Never);
        observation.Verify(x => x.Canceled(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Direct_WhenCanceled_RecordsACancellationNotAFailure()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), pageable: null);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        var executor = new Mock<IQueryContextExecutor<FakeContext>>();
        var observation = new Mock<IQueryExecutionObservation>();
        var sut = CreateSut(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, executor: executor, observation: observation);
        executor.Setup(x => x.ToListAsync(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(new QueryRequest(), registration));

        observation.Verify(x => x.Canceled(), Times.Once);
        observation.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Direct_WhenTheSourceHasNoSourceShape_FailsWithTypeMismatch()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), pageable: null);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new object());
        var observation = new Mock<IQueryExecutionObservation>();

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(
            () => CreateSut<FakeContext>(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, observation: observation)
                .ExecuteAsync(new QueryRequest(), registration));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
        observation.Verify(x => x.ExecutionFailed(exception), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Direct_WhenTheResultTypeIsNotTheQueryContext_FailsWithTypeMismatch()
    {
        var registration = CreateSourceRegistration(typeof(FakeSource), pageable: null);
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        var observation = new Mock<IQueryExecutionObservation>();

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(
            () => CreateSut<FakeView>(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, observation: observation)
                .ExecuteAsync(new QueryRequest(), registration));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
        Assert.Contains(typeof(FakeView).FullName!, exception.Message, StringComparison.Ordinal);
        observation.Verify(x => x.ExecutionFailed(exception), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_View_UsesAnAsyncView()
    {
        var asyncView = new Mock<IQueryViewSourceAsync<FakeSource, FakeContext, FakeView, EmptyQueryViewParameters>>();
        asyncView
            .Setup(x => x.CreateViewAsync(It.IsAny<IQueryable<FakeContext>>(), It.IsAny<QueryExecutionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IQueryable<FakeContext> query, QueryExecutionContext _, CancellationToken _) => query.Select(x => new FakeView { Label = "async-" + x.Code }));
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        serviceProvider.Setup(x => x.GetService(typeof(FakeLabelView))).Returns(asyncView.Object);
        var observation = new Mock<IQueryExecutionObservation>();

        var result = await CreateSut<FakeView>(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, observation: observation)
            .ExecuteAsync(new QueryRequest(), CreateSourceRegistration(typeof(FakeSource), pageable: null), CreateViewRegistration(pageable: null));

        Assert.Equal(["async-A", "async-B"], result.Results.Select(x => x.Label));
        observation.Verify(x => x.BeginView(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_View_WhenTheViewHasNoViewShape_FailsWithTypeMismatch()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        serviceProvider.Setup(x => x.GetService(typeof(FakeLabelView))).Returns(new object());
        var observation = new Mock<IQueryExecutionObservation>();

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(
            () => CreateSut<FakeView>(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, observation: observation)
                .ExecuteAsync(new QueryRequest(), CreateSourceRegistration(typeof(FakeSource), pageable: null), CreateViewRegistration(pageable: null)));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
        Assert.Contains(typeof(FakeLabelView).FullName!, exception.Message, StringComparison.Ordinal);
        observation.Verify(x => x.ExecutionFailed(exception), Times.Once);
        observation.Verify(x => x.Canceled(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_View_WhenValidationFails_RecordsAValidationFailureOnly()
    {
        var validator = new Mock<IQueryContextValidator>();
        var failure = new KaleidoValidationException("qry_bad", "bad");
        validator.Setup(x => x.Validate(It.IsAny<IQueryRequest>(), It.IsAny<QuerySourceMetadata>(), It.IsAny<PageableMetadata?>())).Throws(failure);
        var observation = new Mock<IQueryExecutionObservation>();

        await Assert.ThrowsAsync<KaleidoValidationException>(
            () => CreateSut<FakeView>(validator.Object, Mock.Of<IServiceProvider>(), observation: observation)
                .ExecuteAsync(new QueryRequest(), CreateSourceRegistration(typeof(FakeSource), pageable: null), CreateViewRegistration(pageable: null)));

        observation.Verify(x => x.ValidationFailed(failure), Times.Once);
        observation.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_View_WhenCanceled_RecordsACancellationNotAFailure()
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(FakeSource))).Returns(new FakeSource());
        serviceProvider.Setup(x => x.GetService(typeof(FakeLabelView))).Returns(new FakeLabelView());
        var executor = new Mock<IQueryContextExecutor<FakeView>>();
        var observation = new Mock<IQueryExecutionObservation>();
        var sut = CreateSut(Mock.Of<IQueryContextValidator>(), serviceProvider.Object, executor: executor, observation: observation);
        executor.Setup(x => x.ToListAsync(It.IsAny<IQueryable<FakeView>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.ExecuteAsync(new QueryRequest(), CreateSourceRegistration(typeof(FakeSource), pageable: null), CreateViewRegistration(pageable: null)));

        observation.Verify(x => x.Canceled(), Times.Once);
        observation.Verify(x => x.ExecutionFailed(It.IsAny<Exception>()), Times.Never);
    }

    private static QueryViewRegistration CreateViewRegistration(PageableMetadata? pageable) =>
        new(
            typeof(FakeLabelView),
            typeof(FakeView),
            typeof(EmptyQueryViewParameters),
            typeof(FakeSource),
            typeof(FakeContext),
            new QueryViewMetadata(nameof(FakeLabelView), "1.0.0", "Labels", "Labels.", pageable, [], []));

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

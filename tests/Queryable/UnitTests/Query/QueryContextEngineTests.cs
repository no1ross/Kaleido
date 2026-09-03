using Kaleido.Eventing;
using Kaleido.Observability;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Observability;
using Kaleido.Queryable.Runtime;
using Moq;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class QueryContextEngineTests
{
    [Fact]
    public async Task ExecuteAsync_ForView_RunsPipelineAndMaterializesPage()
    {
        var request = new QueryRequest();
        var registration = CreateContextRegistration();
        var viewRegistration = CreateViewRegistration();
        var compiled = new CompiledRecordQuery(null, null, Array.Empty<CompiledSort>(), new CompiledPage(1, 0));
        var sourceQuery = new[] { new TestContext { Id = 1, Name = "A" } }.AsQueryable();
        var viewQuery = new[] { new TestViewContract { Id = 1 } }.AsQueryable();

        var validator = new Mock<IQueryContextValidator>();
        var compiler = new Mock<IQueryContextCompiler>();
        compiler.Setup(x => x.Compile(request, registration.Metadata, viewRegistration.Metadata)).Returns(compiled);

        var source = new Mock<IQueryContextSource<TestContext>>();
        source.Setup(x => x.CreateQuery(It.IsAny<QueryExecutionContext>())).Returns(sourceQuery);

        var applier = new Mock<ICompiledQueryApplier<TestContext>>();
        applier.Setup(x => x.ApplySearch(sourceQuery, compiled.Search)).Returns(sourceQuery);
        applier.Setup(x => x.ApplyFilter(sourceQuery, compiled.Filter)).Returns(sourceQuery);
        applier.Setup(x => x.ApplySort(sourceQuery, compiled.Sort)).Returns(sourceQuery);

        var executor = new Mock<IQueryContextExecutor<TestViewContract>>();
        executor.Setup(x => x.CountAsync(viewQuery, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        executor.Setup(x => x.ApplyPage(viewQuery, compiled.Page)).Returns(viewQuery);
        executor.Setup(x => x.ToListAsync(viewQuery, It.IsAny<CancellationToken>())).ReturnsAsync(viewQuery.ToList());

        var services = new ServiceCollection();
        services.AddScoped(typeof(TestQueryView), _ => new TestQueryView(viewQuery));
        using var provider = services.BuildServiceProvider();

        var engine = new QueryContextEngine<TestContext, TestViewContract>(
            validator.Object,
            compiler.Object,
            source.Object,
            applier.Object,
            executor.Object,
            CreateQueryEventFactory().Object,
            CreateEventPublisher().Object,
            CreateCorrelationAccessor().Object,
            new TestQueryableObservability(),
            provider);

        var result = await engine.ExecuteAsync(request, registration, viewRegistration);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Records);
        validator.Verify(x => x.Validate(request, registration, viewRegistration), Times.Once);
        executor.Verify(x => x.ApplyPage(viewQuery, compiled.Page), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ForDirectQuery_WhenResultTypeDoesNotMatch_Throws()
    {
        var request = new QueryRequest();
        var registration = CreateContextRegistration();
        var compiled = new CompiledRecordQuery(null, null, Array.Empty<CompiledSort>(), new CompiledPage(1, 0));
        var sourceQuery = new[] { new TestContext { Id = 1, Name = "A" } }.AsQueryable();

        var validator = new Mock<IQueryContextValidator>();
        var compiler = new Mock<IQueryContextCompiler>();
        compiler.Setup(x => x.Compile(request, registration.Metadata)).Returns(compiled);

        var source = new Mock<IQueryContextSource<TestContext>>();
        source.Setup(x => x.CreateQuery(It.IsAny<QueryExecutionContext>())).Returns(sourceQuery);

        var applier = new Mock<ICompiledQueryApplier<TestContext>>();
        applier.Setup(x => x.ApplySearch(sourceQuery, compiled.Search)).Returns(sourceQuery);
        applier.Setup(x => x.ApplyFilter(sourceQuery, compiled.Filter)).Returns(sourceQuery);
        applier.Setup(x => x.ApplySort(sourceQuery, compiled.Sort)).Returns(sourceQuery);

        var executor = new Mock<IQueryContextExecutor<TestViewContract>>();

        var engine = new QueryContextEngine<TestContext, TestViewContract>(
            validator.Object,
            compiler.Object,
            source.Object,
            applier.Object,
            executor.Object,
            CreateQueryEventFactory().Object,
            CreateEventPublisher().Object,
            CreateCorrelationAccessor().Object,
            new TestQueryableObservability(),
            new ServiceCollection().BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.ExecuteAsync(request, registration));

        Assert.Contains("requires result type", exception.Message);
    }

    private static Mock<IEventPublisher> CreateEventPublisher()
    {
        var publisher =
            new Mock<IEventPublisher>();

        publisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<QueryExecuted>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return publisher;
    }

    private static Mock<IKaleidoCorrelationContextAccessor> CreateCorrelationAccessor()
    {
        var accessor =
            new Mock<IKaleidoCorrelationContextAccessor>();

        accessor
            .SetupGet(x => x.Current)
            .Returns(new KaleidoCorrelationContext());

        return accessor;
    }

    private static Mock<IQueryEventFactory> CreateQueryEventFactory()
    {
        var factory =
            new Mock<IQueryEventFactory>();

        factory
            .Setup(x =>
                x.CreateQueryExecuted(
                    It.IsAny<KaleidoCorrelationContext>(),
                    It.IsAny<QueryObservationDetails>(),
                    It.IsAny<IQueryRequest>(),
                    It.IsAny<QueryResult<TestViewContract>>()))
            .Returns<KaleidoCorrelationContext, QueryObservationDetails, IQueryRequest, QueryResult<TestViewContract>>((correlation, details, request, result) =>
                new Eventing.QueryExecuted
                {
                    ProcessId = correlation.ProcessId,
                    OccurredOn = DateTimeOffset.UtcNow,
                    QueryContextName = details.QueryContextName,
                    QueryViewName = details.QueryViewName,
                    IsDirectQuery = details.IsDirectQuery,
                    Request = request,
                    TotalCount = result.TotalCount,
                    ReturnedCount = result.Records.Count,
                    PageSize = result.PageSize,
                    Offset = result.Offset,
                    Records = result.Records.Cast<object?>().ToArray(),
                    SearchText = request.Query?.SearchText,
                    SortCount = request.Query?.Sort?.Count ?? 0,
                    FilterProvided = request.Query?.Filter is not null,
                    ViewParameters = request.ViewParameters
                });

        return factory;
    }

    private static QueryContextRegistration CreateContextRegistration() =>
        new(
            typeof(TestContext),
            typeof(TestContextSource),
            new QueryContextMetadata(
                "test-context",
                "Test Context",
                "Test Context",
                "1.0.0",
                "Unit Test",
                QueryContextKind.Direct,
                new PageableMetadata(25, 100),
                []));

    private static QueryViewRegistration CreateViewRegistration() =>
        new(
            typeof(TestQueryView),
            typeof(TestViewContract),
            typeof(EmptyQueryViewParameters),
            typeof(TestContext),
            new QueryViewMetadata(
                "test-view",
                "1.0.0",
                "Test View",
                "Test View",
                QueryViewVisibility.Public,
                new PageableMetadata(25, 100),
                [],
                []));

    public sealed class TestContext
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed class TestViewContract
    {
        public int Id { get; init; }
    }

    private sealed class TestContextSource : IQueryContextSource<TestContext>
    {
        public IQueryable<TestContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestContext>().AsQueryable();
    }

    private sealed class TestQueryView : IQueryViewSource<TestContext, TestViewContract>
    {
        private readonly IQueryable<TestViewContract> _result;

        public TestQueryView(IQueryable<TestViewContract> result)
        {
            _result = result;
        }

        public IQueryable<TestViewContract> CreateView(IQueryable<TestContext> query, QueryExecutionContext executionContext) => _result;
    }

    private sealed class TestQueryableObservability
        : IQueryableObservability
    {
        public IQueryExecutionObservation BeginExecution(
            QueryObservationDetails details)
        {
            return new TestQueryExecutionObservation();
        }
    }

    private sealed class TestQueryExecutionObservation
        : IQueryExecutionObservation
    {
        public IDisposable BeginSource()
        {
            return NullScope.Instance;
        }

        public IDisposable BeginView()
        {
            return NullScope.Instance;
        }

        public IDisposable BeginMaterialization()
        {
            return NullScope.Instance;
        }

        public IDisposable BeginDelegate()
        {
            return NullScope.Instance;
        }

        public void ValidationFailed(
            Queryable.Exceptions.QueryableValidationException exception)
        {
        }

        public void Materialized(
            int totalCount,
            int returnedCount,
            int? pageSize,
            int? pageOffset)
        {
        }

        public void ExecutionFailed(
            Exception exception)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class NullScope
        : IDisposable
    {
        public static readonly NullScope Instance =
            new();

        public void Dispose()
        {
        }
    }
}

using Kaleido.Metadata;
using Kaleido.Queryable;
using Kaleido.Validation;
using Moq;
using Xunit;

namespace Kaleido.UnitTests;

public sealed class QueryableRecordQueryEngineTests
{
    private readonly Fixture _fixture;
    private readonly QueryableRecordQueryEngine<TestRecord> _sut;

    public QueryableRecordQueryEngineTests()
    {
        _fixture = new Fixture();

        _sut = new QueryableRecordQueryEngine<TestRecord>(
            _fixture.MetadataCatalog.Object,
            _fixture.Validator.Object,
            _fixture.Compiler.Object,
            _fixture.Source.Object,
            _fixture.NamedQueries,
            _fixture.Applier.Object,
            _fixture.Executor.Object);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Get_Metadata()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        _fixture.SetupSuccessfulPipeline(request, compiled);

        await _sut.ExecuteTypedAsync(request);

        _fixture.MetadataCatalog.Verify(
            x => x.GetMetadata<TestRecord>(),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Validate_Request()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        _fixture.SetupSuccessfulPipeline(request, compiled);

        await _sut.ExecuteTypedAsync(request);

        _fixture.Validator.Verify(
            x => x.Validate(
                request,
                TestData.Metadata),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Compile_Request()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        _fixture.SetupSuccessfulPipeline(request, compiled);

        await _sut.ExecuteTypedAsync(request);

        _fixture.Compiler.Verify(
            x => x.Compile(
                request,
                TestData.Metadata),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Create_Source_Query_With_Context()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        _fixture.SetupSuccessfulPipeline(request, compiled);

        await _sut.ExecuteTypedAsync(request);

        _fixture.Source.Verify(
            x => x.CreateQuery(
                It.Is<RecordExecutionContext>(context =>
                    ReferenceEquals(context.Metadata, TestData.Metadata) &&
                    ReferenceEquals(context.Request, request))),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Not_Apply_NamedQuery_When_Not_Specified()
    {
        var request = TestData.Request();

        var compiled = TestData.CompiledQuery(
            namedQuery: null);

        var namedQuery = new Mock<IQueryableRecordNamedQuery<TestRecord>>();

        namedQuery
            .SetupGet(x => x.Name)
            .Returns("active-records");

        _fixture.NamedQueries.Add(namedQuery.Object);

        _fixture.SetupSuccessfulPipeline(request, compiled);

        await _sut.ExecuteTypedAsync(request);

        namedQuery.Verify(
            x => x.Apply(
                It.IsAny<IQueryable<TestRecord>>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Apply_NamedQuery_When_Specified()
    {
        var request = TestData.Request();

        var parameters =
            new Dictionary<string, object?>
            {
                ["category"] = "Alpha"
            };

        var compiled = TestData.CompiledQuery(
            namedQuery: "records-by-category",
            parameters: parameters);

        var namedQueryResult =
            TestData.Query(10, 20, 30);

        var namedQuery = new Mock<IQueryableRecordNamedQuery<TestRecord>>();

        namedQuery
            .SetupGet(x => x.Name)
            .Returns("records-by-category");

        namedQuery
            .Setup(x => x.Apply(
                TestData.SourceQuery,
                parameters))
            .Returns(namedQueryResult);

        _fixture.NamedQueries.Add(namedQuery.Object);

        _fixture.SetupSuccessfulPipeline(
            request,
            compiled,
            sourceQuery: TestData.SourceQuery,
            namedQueryResult: namedQueryResult);

        await _sut.ExecuteTypedAsync(request);

        namedQuery.Verify(
            x => x.Apply(
                TestData.SourceQuery,
                parameters),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Throw_When_NamedQuery_Handler_Is_Missing()
    {
        var request = TestData.Request();

        var compiled = TestData.CompiledQuery(
            namedQuery: "missing-query");

        _fixture.MetadataCatalog
            .Setup(x => x.GetMetadata<TestRecord>())
            .Returns(TestData.Metadata);

        _fixture.Validator
            .Setup(x => x.Validate(
                request,
                TestData.Metadata));

        _fixture.Compiler
            .Setup(x => x.Compile(
                request,
                TestData.Metadata))
            .Returns(compiled);

        _fixture.Source
            .Setup(x => x.CreateQuery(
                It.IsAny<RecordExecutionContext>()))
            .Returns(TestData.SourceQuery);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sut.ExecuteTypedAsync(request));

        Assert.Contains(
            "missing-query",
            exception.Message);

        _fixture.Applier.Verify(
            x => x.ApplyFilter(
                It.IsAny<IQueryable<TestRecord>>(),
                It.IsAny<CompiledFilterExpression?>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Apply_Filter_Search_And_Sort()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        _fixture.SetupSuccessfulPipeline(request, compiled);

        await _sut.ExecuteTypedAsync(request);

        _fixture.Applier.Verify(
            x => x.ApplyFilter(
                TestData.SourceQuery,
                compiled.Filter),
            Times.Once);

        _fixture.Applier.Verify(
            x => x.ApplySearch(
                TestData.FilteredQuery,
                compiled.Search),
            Times.Once);

        _fixture.Applier.Verify(
            x => x.ApplySort(
                TestData.SearchedQuery,
                compiled.Sort),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Count_Before_Paging()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        _fixture.SetupSuccessfulPipeline(request, compiled);

        var result =
            await _sut.ExecuteTypedAsync(request);

        Assert.Equal(
            TestData.TotalCount,
            result.TotalCount);

        _fixture.Executor.Verify(
            x => x.CountAsync(
                TestData.SortedQuery,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _fixture.Applier.Verify(
            x => x.ApplyPage(
                TestData.SortedQuery,
                compiled.Page),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Materialize_After_Paging()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        _fixture.SetupSuccessfulPipeline(request, compiled);

        await _sut.ExecuteTypedAsync(request);

        _fixture.Executor.Verify(
            x => x.ToListAsync(
                TestData.PagedQuery,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Return_QueryResult_With_Items_TotalCount_And_Metadata()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        _fixture.SetupSuccessfulPipeline(request, compiled);

        var result =
            await _sut.ExecuteTypedAsync(request);

        Assert.Equal(
            TestData.Items,
            result.Items);

        Assert.Equal(
            TestData.TotalCount,
            result.TotalCount);

        Assert.Same(
            TestData.Metadata,
            result.RuntimeMetadata);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Return_RecordQueryResult()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        _fixture.SetupSuccessfulPipeline(request, compiled);

        var result =
            await _sut.ExecuteAsync(request);

        Assert.IsAssignableFrom<IRecordQueryResult>(
            result);

        Assert.Equal(
            TestData.TotalCount,
            result.TotalCount);
    }

    [Fact]
    public async Task ExecuteTypedAsync_Should_Pass_CancellationToken_To_Executor()
    {
        var request = TestData.Request();
        var compiled = TestData.CompiledQuery();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        var cancellationToken =
            cancellationTokenSource.Token;

        _fixture.SetupSuccessfulPipeline(request, compiled);

        await _sut.ExecuteTypedAsync(
            request,
            cancellationToken);

        _fixture.Executor.Verify(
            x => x.CountAsync(
                It.IsAny<IQueryable<TestRecord>>(),
                cancellationToken),
            Times.Once);

        _fixture.Executor.Verify(
            x => x.ToListAsync(
                It.IsAny<IQueryable<TestRecord>>(),
                cancellationToken),
            Times.Once);
    }

    private sealed class Fixture
    {
        public Mock<IRecordMetadataCatalog> MetadataCatalog { get; } =
            new();

        public Mock<IRecordQueryValidator> Validator { get; } =
            new();

        public Mock<IRecordQueryCompiler> Compiler { get; } =
            new();

        public Mock<IQueryableRecordSource<TestRecord>> Source { get; } =
            new();

        public List<IQueryableRecordNamedQuery<TestRecord>> NamedQueries { get; } =
            [];

        public Mock<IQueryableCompiledQueryApplier<TestRecord>> Applier { get; } =
            new();

        public Mock<IQueryableRecordExecutor<TestRecord>> Executor { get; } =
            new();

        public void SetupSuccessfulPipeline(
            KaleidoQueryRequest request,
            CompiledRecordQuery compiled,
            IQueryable<TestRecord>? sourceQuery = null,
            IQueryable<TestRecord>? namedQueryResult = null)
        {
            var source =
                sourceQuery ?? TestData.SourceQuery;

            var afterNamedQuery =
                namedQueryResult ?? source;

            MetadataCatalog
                .Setup(x => x.GetMetadata<TestRecord>())
                .Returns(TestData.Metadata);

            Validator
                .Setup(x => x.Validate(
                    request,
                    TestData.Metadata));

            Compiler
                .Setup(x => x.Compile(
                    request,
                    TestData.Metadata))
                .Returns(compiled);

            Source
                .Setup(x => x.CreateQuery(
                    It.IsAny<RecordExecutionContext>()))
                .Returns(source);

            Applier
                .Setup(x => x.ApplyFilter(
                    afterNamedQuery,
                    compiled.Filter))
                .Returns(TestData.FilteredQuery);

            Applier
                .Setup(x => x.ApplySearch(
                    TestData.FilteredQuery,
                    compiled.Search))
                .Returns(TestData.SearchedQuery);

            Applier
                .Setup(x => x.ApplySort(
                    TestData.SearchedQuery,
                    compiled.Sort))
                .Returns(TestData.SortedQuery);

            Executor
                .Setup(x => x.CountAsync(
                    TestData.SortedQuery,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.TotalCount);

            Applier
                .Setup(x => x.ApplyPage(
                    TestData.SortedQuery,
                    compiled.Page))
                .Returns(TestData.PagedQuery);

            Executor
                .Setup(x => x.ToListAsync(
                    TestData.PagedQuery,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestData.Items);
        }
    }

    private static class TestData
    {
        public const int TotalCount = 100;

        public static readonly RuntimeRecordMetadata Metadata =
            new(
                Name: "test-record",
                Version: "1.0.0",
                Source: "Unit Test",
                Fields: [],
                AllowedQueries: [],
                Pageable: null);

        public static readonly IReadOnlyList<TestRecord> Items =
        [
            new() { Id = 1 },
            new() { Id = 2 }
        ];

        public static readonly IQueryable<TestRecord> SourceQuery =
            Query(1, 2, 3);

        public static readonly IQueryable<TestRecord> FilteredQuery =
            Query(1, 2);

        public static readonly IQueryable<TestRecord> SearchedQuery =
            Query(1, 2);

        public static readonly IQueryable<TestRecord> SortedQuery =
            Query(2, 1);

        public static readonly IQueryable<TestRecord> PagedQuery =
            Query(2);

        public static KaleidoQueryRequest Request()
        {
            return new(
                QueryName: null,
                Query: null,
                Parameters: null);
        }

        public static IQueryable<TestRecord> Query(
            params int[] ids)
        {
            return ids
                .Select(x => new TestRecord { Id = x })
                .AsQueryable();
        }

        public static CompiledRecordQuery CompiledQuery(
            string? namedQuery = null,
            IReadOnlyDictionary<string, object?>? parameters = null)
        {
            return new CompiledRecordQuery(
                NamedQuery: namedQuery,
                Parameters: parameters,
                Filter: null,
                Search: null,
                Sort: [],
                Page: new CompiledPage(
                    Offset: 0,
                    Size: 25));
        }
    }

    public sealed class TestRecord
    {
        public int Id { get; init; }
    }
}
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Moq;
using Xunit;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class RecordQueryEngineTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRunQueryPipelineAndReturnResult()
    {
        // Arrange
        var request =
            new QueryRequest();

        var metadata =
            CreateMetadata();

        var registration =
            CreateRegistration(
                metadata);

        var compiled =
            CreateCompiledQuery();

        var sourceQuery =
            new[]
            {
            new TestRecord(1, "A"),
            new TestRecord(2, "B")
            }
            .AsQueryable();

        var filteredQuery =
            sourceQuery
                .Where(x => x.Id > 0);

        var searchedQuery =
            filteredQuery
                .Where(x => x.Name != string.Empty);

        var sortedQuery =
            searchedQuery
                .OrderBy(x => x.Id);

        var pagedQuery =
            sortedQuery
                .Skip(0)
                .Take(1);

        var records =
            new List<TestRecord>
            {
            new(1, "A")
            };

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.GetRegistration(typeof(TestRecord)))
            .Returns(registration);

        var validator =
            new Mock<IRecordQueryValidator>();

        var compiler =
            new Mock<IRecordQueryCompiler>();

        compiler
            .Setup(x => x.Compile(request, metadata))
            .Returns(compiled);

        var source =
            new Mock<IRecordSource<TestRecord>>();

        source
            .Setup(x => x.CreateQuery(
                It.Is<RecordExecutionContext>(
                    context =>
                        ReferenceEquals(context.Metadata, metadata) &&
                        ReferenceEquals(context.Request, request))))
            .Returns(sourceQuery);

        var applier =
            new Mock<ICompiledQueryApplier<TestRecord>>();

        applier
            .Setup(x => x.ApplyFilter(sourceQuery, compiled.Filter))
            .Returns(filteredQuery);

        applier
            .Setup(x => x.ApplySearch(filteredQuery, compiled.Search))
            .Returns(searchedQuery);

        applier
            .Setup(x => x.ApplySort(searchedQuery, compiled.Sort))
            .Returns(sortedQuery);

        applier
            .Setup(x => x.ApplyPage(sortedQuery, compiled.Page))
            .Returns(pagedQuery);

        var executor =
            new Mock<IRecordExecutor<TestRecord>>();

        executor
            .Setup(x => x.CountAsync(
                sortedQuery,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        executor
            .Setup(x => x.ToListAsync(
                pagedQuery,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var engine =
            new RecordQueryEngine<TestRecord>(
                registry.Object,
                validator.Object,
                compiler.Object,
                source.Object,
                [],
                applier.Object,
                executor.Object);

        // Act
        var result =
            await engine.ExecuteAsync(
                request);

        // Assert
        Assert.Equal(
            10,
            result.TotalCount);

        Assert.Equal(
            compiled.Page?.Offset ?? 0,
            result.Offset);

        Assert.Equal(
            compiled.Page?.Size ?? records.Count,
            result.PageSize);

        Assert.Same(
            records,
            result.Records);

        validator.Verify(
            x => x.Validate(request, registration),
            Times.Once);

        compiler.Verify(
            x => x.Compile(request, metadata),
            Times.Once);

        source.Verify(
            x => x.CreateQuery(
                It.IsAny<RecordExecutionContext>()),
            Times.Once);

        applier.Verify(
            x => x.ApplyFilter(sourceQuery, compiled.Filter),
            Times.Once);

        applier.Verify(
            x => x.ApplySearch(filteredQuery, compiled.Search),
            Times.Once);

        applier.Verify(
            x => x.ApplySort(searchedQuery, compiled.Sort),
            Times.Once);

        applier.Verify(
            x => x.ApplyPage(sortedQuery, compiled.Page),
            Times.Once);

        executor.Verify(
            x => x.CountAsync(
                sortedQuery,
                It.IsAny<CancellationToken>()),
            Times.Once);

        executor.Verify(
            x => x.ToListAsync(
                pagedQuery,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassCancellationTokenToExecutor()
    {
        // Arrange
        var request =
            new QueryRequest();

        var metadata =
            CreateMetadata();

        var registration =
            CreateRegistration(
                metadata);

        var compiled =
            CreateCompiledQuery();

        var query =
            new[]
            {
                new TestRecord(1, "A")
            }
            .AsQueryable();

        var cancellationToken =
            new CancellationTokenSource().Token;

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.GetRegistration(typeof(TestRecord)))
            .Returns(registration);

        var validator =
            new Mock<IRecordQueryValidator>();

        var compiler =
            new Mock<IRecordQueryCompiler>();

        compiler
            .Setup(x => x.Compile(request, metadata))
            .Returns(compiled);

        var source =
            new Mock<IRecordSource<TestRecord>>();

        source
            .Setup(x => x.CreateQuery(It.IsAny<RecordExecutionContext>()))
            .Returns(query);

        var applier =
            new Mock<ICompiledQueryApplier<TestRecord>>();

        applier
            .Setup(x => x.ApplyFilter(query, compiled.Filter))
            .Returns(query);

        applier
            .Setup(x => x.ApplySearch(query, compiled.Search))
            .Returns(query);

        applier
            .Setup(x => x.ApplySort(query, compiled.Sort))
            .Returns(query);

        applier
            .Setup(x => x.ApplyPage(query, compiled.Page))
            .Returns(query);

        var executor =
            new Mock<IRecordExecutor<TestRecord>>();

        executor
            .Setup(x => x.CountAsync(query, cancellationToken))
            .ReturnsAsync(1);

        executor
            .Setup(x => x.ToListAsync(query, cancellationToken))
            .ReturnsAsync(
                [
                    new TestRecord(1, "A")
                ]);

        var engine =
            new RecordQueryEngine<TestRecord>(
                registry.Object,
                validator.Object,
                compiler.Object,
                source.Object,
                [],
                applier.Object,
                executor.Object);

        // Act
        await engine.ExecuteAsync(
            request,
            cancellationToken);

        // Assert
        executor.Verify(
            x => x.CountAsync(
                query,
                cancellationToken),
            Times.Once);

        executor.Verify(
            x => x.ToListAsync(
                query,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldApplyNamedQuery_WhenCompiledNamedQueryExists()
    {
        // Arrange
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "active"));

        var metadata =
            CreateMetadata();

        var namedQueryRegistration =
            new NamedQueryRegistration(
                typeof(TestNamedQuery),
                new NamedQueryMetadata(
                    "active",
                    "Active records",
                    "Active records",
                    null));

        var registration =
            CreateRegistration(
                metadata,
                [namedQueryRegistration]);

        var compiled =
            CreateCompiledQuery(
                new NamedQuery(
                    "active"));

        var sourceQuery =
            new[]
            {
                new TestRecord(1, "A"),
                new TestRecord(2, "B")
            }
            .AsQueryable();

        var namedQueryResult =
            sourceQuery
                .Where(x => x.Id == 1);

        var handler =
            new TestNamedQuery
            {
                QueryToReturn = namedQueryResult
            };

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.GetRegistration(typeof(TestRecord)))
            .Returns(registration);

        var validator =
            new Mock<IRecordQueryValidator>();

        var compiler =
            new Mock<IRecordQueryCompiler>();

        compiler
            .Setup(x => x.Compile(request, metadata))
            .Returns(compiled);

        var source =
            new Mock<IRecordSource<TestRecord>>();

        source
            .Setup(x => x.CreateQuery(It.IsAny<RecordExecutionContext>()))
            .Returns(sourceQuery);

        var applier =
            new Mock<ICompiledQueryApplier<TestRecord>>();

        applier
            .Setup(x => x.ApplyFilter(namedQueryResult, compiled.Filter))
            .Returns(namedQueryResult);

        applier
            .Setup(x => x.ApplySearch(namedQueryResult, compiled.Search))
            .Returns(namedQueryResult);

        applier
            .Setup(x => x.ApplySort(namedQueryResult, compiled.Sort))
            .Returns(namedQueryResult);

        applier
            .Setup(x => x.ApplyPage(namedQueryResult, compiled.Page))
            .Returns(namedQueryResult);

        var executor =
            new Mock<IRecordExecutor<TestRecord>>();

        executor
            .Setup(x => x.CountAsync(namedQueryResult, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        executor
            .Setup(x => x.ToListAsync(namedQueryResult, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    new TestRecord(1, "A")
                ]);

        var engine =
            new RecordQueryEngine<TestRecord>(
                registry.Object,
                validator.Object,
                compiler.Object,
                source.Object,
                [handler],
                applier.Object,
                executor.Object);

        // Act
        await engine.ExecuteAsync(
            request);

        // Assert
        Assert.True(
            handler.WasApplied);

        Assert.Same(
            sourceQuery,
            handler.ReceivedQuery);

        Assert.Same(
            compiled.NamedQuery,
            handler.ReceivedNamedQuery);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipNamedQuery_WhenCompiledNamedQueryIsNull()
    {
        // Arrange
        var request =
            new QueryRequest();

        var metadata =
            CreateMetadata();

        var registration =
            CreateRegistration(
                metadata);

        var compiled =
            CreateCompiledQuery();

        var query =
            new[]
            {
                new TestRecord(1, "A")
            }
            .AsQueryable();

        var handler =
            new TestNamedQuery();

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.GetRegistration(typeof(TestRecord)))
            .Returns(registration);

        var validator =
            new Mock<IRecordQueryValidator>();

        var compiler =
            new Mock<IRecordQueryCompiler>();

        compiler
            .Setup(x => x.Compile(request, metadata))
            .Returns(compiled);

        var source =
            new Mock<IRecordSource<TestRecord>>();

        source
            .Setup(x => x.CreateQuery(It.IsAny<RecordExecutionContext>()))
            .Returns(query);

        var applier =
            new Mock<ICompiledQueryApplier<TestRecord>>();

        applier
            .Setup(x => x.ApplyFilter(query, compiled.Filter))
            .Returns(query);

        applier
            .Setup(x => x.ApplySearch(query, compiled.Search))
            .Returns(query);

        applier
            .Setup(x => x.ApplySort(query, compiled.Sort))
            .Returns(query);

        applier
            .Setup(x => x.ApplyPage(query, compiled.Page))
            .Returns(query);

        var executor =
            new Mock<IRecordExecutor<TestRecord>>();

        executor
            .Setup(x => x.CountAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        executor
            .Setup(x => x.ToListAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    new TestRecord(1, "A")
                ]);

        var engine =
            new RecordQueryEngine<TestRecord>(
                registry.Object,
                validator.Object,
                compiler.Object,
                source.Object,
                [handler],
                applier.Object,
                executor.Object);

        // Act
        await engine.ExecuteAsync(
            request);

        // Assert
        Assert.False(
            handler.WasApplied);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenNamedQueryRegistrationDoesNotExist()
    {
        // Arrange
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "missing"));

        var metadata =
            CreateMetadata();

        var registration =
            CreateRegistration(
                metadata);

        var compiled =
            CreateCompiledQuery(
                new NamedQuery(
                    "missing"));

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.GetRegistration(typeof(TestRecord)))
            .Returns(registration);

        var validator =
            new Mock<IRecordQueryValidator>();

        var compiler =
            new Mock<IRecordQueryCompiler>();

        compiler
            .Setup(x => x.Compile(request, metadata))
            .Returns(compiled);

        var source =
            new Mock<IRecordSource<TestRecord>>();

        source
            .Setup(x => x.CreateQuery(It.IsAny<RecordExecutionContext>()))
            .Returns(
                Array.Empty<TestRecord>()
                    .AsQueryable());

        var engine =
            new RecordQueryEngine<TestRecord>(
                registry.Object,
                validator.Object,
                compiler.Object,
                source.Object,
                [],
                Mock.Of<ICompiledQueryApplier<TestRecord>>(),
                Mock.Of<IRecordExecutor<TestRecord>>());

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.ExecuteAsync(
                request));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenNamedQueryHandlerIsNotRegistered()
    {
        // Arrange
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "active"));

        var metadata =
            CreateMetadata();

        var registration =
            CreateRegistration(
                metadata,
                [
                    new NamedQueryRegistration(
                        typeof(TestNamedQuery),
                        new NamedQueryMetadata(
                            "active",
                            "Active records",
                            "Active records",
                            null))
                ]);

        var compiled =
            CreateCompiledQuery(
                new NamedQuery(
                    "active"));

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.GetRegistration(typeof(TestRecord)))
            .Returns(registration);

        var validator =
            new Mock<IRecordQueryValidator>();

        var compiler =
            new Mock<IRecordQueryCompiler>();

        compiler
            .Setup(x => x.Compile(request, metadata))
            .Returns(compiled);

        var source =
            new Mock<IRecordSource<TestRecord>>();

        source
            .Setup(x => x.CreateQuery(It.IsAny<RecordExecutionContext>()))
            .Returns(
                Array.Empty<TestRecord>()
                    .AsQueryable());

        var engine =
            new RecordQueryEngine<TestRecord>(
                registry.Object,
                validator.Object,
                compiler.Object,
                source.Object,
                [],
                Mock.Of<ICompiledQueryApplier<TestRecord>>(),
                Mock.Of<IRecordExecutor<TestRecord>>());

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.ExecuteAsync(
                request));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountBeforePaging()
    {
        // Arrange
        var request =
            new QueryRequest();

        var metadata =
            CreateMetadata();

        var registration =
            CreateRegistration(
                metadata);

        var compiled =
            CreateCompiledQuery();

        var sourceQuery =
            Enumerable.Range(1, 5)
                .Select(x => new TestRecord(x, $"Record {x}"))
                .AsQueryable();

        var sortedQuery =
            sourceQuery
                .OrderBy(x => x.Id);

        var pagedQuery =
            sortedQuery
                .Skip(0)
                .Take(2);

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .Setup(x => x.GetRegistration(typeof(TestRecord)))
            .Returns(registration);

        var validator =
            new Mock<IRecordQueryValidator>();

        var compiler =
            new Mock<IRecordQueryCompiler>();

        compiler
            .Setup(x => x.Compile(request, metadata))
            .Returns(compiled);

        var source =
            new Mock<IRecordSource<TestRecord>>();

        source
            .Setup(x => x.CreateQuery(It.IsAny<RecordExecutionContext>()))
            .Returns(sourceQuery);

        var applier =
            new Mock<ICompiledQueryApplier<TestRecord>>();

        applier
            .Setup(x => x.ApplyFilter(sourceQuery, compiled.Filter))
            .Returns(sourceQuery);

        applier
            .Setup(x => x.ApplySearch(sourceQuery, compiled.Search))
            .Returns(sourceQuery);

        applier
            .Setup(x => x.ApplySort(sourceQuery, compiled.Sort))
            .Returns(sortedQuery);

        applier
            .Setup(x => x.ApplyPage(sortedQuery, compiled.Page))
            .Returns(pagedQuery);

        var executor =
            new Mock<IRecordExecutor<TestRecord>>();

        executor
            .Setup(x => x.CountAsync(sortedQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        executor
            .Setup(x => x.ToListAsync(pagedQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                [
                    new TestRecord(1, "Record 1"),
                    new TestRecord(2, "Record 2")
                ]);

        var engine =
            new RecordQueryEngine<TestRecord>(
                registry.Object,
                validator.Object,
                compiler.Object,
                source.Object,
                [],
                applier.Object,
                executor.Object);

        // Act
        var result =
            await engine.ExecuteAsync(
                request);

        // Assert
        Assert.Equal(
            5,
            result.TotalCount);

        Assert.Equal(
            2,
            result.Records.Count);

        executor.Verify(
            x => x.CountAsync(
                sortedQuery,
                It.IsAny<CancellationToken>()),
            Times.Once);

        executor.Verify(
            x => x.ToListAsync(
                pagedQuery,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static CompiledRecordQuery CreateCompiledQuery(
        NamedQuery? namedQuery = null)
    {
        return new CompiledRecordQuery(
            namedQuery,
            null,
            null,
            [],
            new CompiledPage(
                25,
                0));
    }

    private static RecordRegistration CreateRegistration(
        RecordMetadata metadata,
        IReadOnlyCollection<NamedQueryRegistration>? namedQueries = null)
    {
        return new RecordRegistration(
            typeof(TestRecord),
            typeof(TestRecordSource),
            metadata,
            namedQueries ?? []);
    }

    private static RecordMetadata CreateMetadata()
    {
        return new RecordMetadata(
            "test-record",
            "Test Record",
            "Test Record",
            "1.0.0",
            "Unit Test",
            [],
            new PageableMetadata(
                25,
                100));
    }

    public sealed record TestRecord(
        int Id,
        string Name);

    private sealed class TestRecordSource
    {
    }

    private sealed class TestNamedQuery
        : IRecordNamedQuery<TestRecord>
    {
        public bool WasApplied { get; private set; }

        public IQueryable<TestRecord>? ReceivedQuery { get; private set; }

        public NamedQuery? ReceivedNamedQuery { get; private set; }

        public IQueryable<TestRecord>? QueryToReturn { get; init; }

        public IQueryable<TestRecord> Apply(
            IQueryable<TestRecord> query,
            NamedQuery namedQuery)
        {
            WasApplied = true;
            ReceivedQuery = query;
            ReceivedNamedQuery = namedQuery;

            return QueryToReturn ?? query;
        }
    }
}
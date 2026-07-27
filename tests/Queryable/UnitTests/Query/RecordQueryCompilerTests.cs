using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Xunit;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class QueryRequestCompilerTests
{
    private readonly QueryRequestCompiler _compiler = new();

    [Fact]
    public void Compile_ShouldThrow_WhenRequestIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => _compiler.Compile(
                null!,
                CreateMetadata()));
    }

    [Fact]
    public void Compile_ShouldThrow_WhenMetadataIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => _compiler.Compile(
                new QueryRequest(),
                null!));
    }

    [Fact]
    public void Compile_ShouldUseMetadataDefaultPageSize_WhenPageSizeIsNotSpecified()
    {
        var result =
            _compiler.Compile(
                new QueryRequest(),
                CreateMetadata());

        Assert.Equal(
            25,
            result.Page.Size);

        Assert.Equal(
            0,
            result.Page.Offset);
    }

    [Fact]
    public void Compile_ShouldUseFallbackPageSize_WhenMetadataDoesNotHavePageable()
    {
        var result =
            _compiler.Compile(
                new QueryRequest(),
                CreateMetadataWithoutPageable());

        Assert.Equal(
            50,
            result.Page.Size);

        Assert.Equal(
            0,
            result.Page.Offset);
    }

    [Fact]
    public void Compile_ShouldClampPageSize_ToMetadataMaxSize()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Page: new QueryPage(
                        500,
                        0)));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        Assert.Equal(
            100,
            result.Page.Size);

        Assert.Equal(
            0,
            result.Page.Offset);
    }

    [Fact]
    public void Compile_ShouldUseRequestedPageSize_WhenWithinMaxSize()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Page: new QueryPage(
                        10,
                        20)));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        Assert.Equal(
            10,
            result.Page.Size);

        Assert.Equal(
            20,
            result.Page.Offset);
    }

    [Fact]
    public void Compile_ShouldUseZeroOffset_WhenOffsetIsNotSpecified()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Page: new QueryPage(
                        10,
                        null)));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        Assert.Equal(
            10,
            result.Page.Size);

        Assert.Equal(
            0,
            result.Page.Offset);
    }

    [Fact]
    public void Compile_ShouldCompileNamedQuery()
    {
        var parameters =
            new Dictionary<string, object?>
            {
                ["Category"] = "A"
            };

        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "by-category",
                    parameters));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        Assert.NotNull(
            result.NamedQuery);

        Assert.Equal(
            "by-category",
            result.NamedQuery!.Name);

        Assert.Same(
            parameters,
            result.NamedQuery.Parameters);
    }

    [Fact]
    public void Compile_ShouldReturnNullNamedQuery_WhenRequestDoesNotHaveNamedQuery()
    {
        var result =
            _compiler.Compile(
                new QueryRequest(),
                CreateMetadata());

        Assert.Null(
            result.NamedQuery);
    }

    [Fact]
    public void Compile_ShouldReturnNullFilter_WhenRequestDoesNotHaveFilter()
    {
        var result =
            _compiler.Compile(
                new QueryRequest(),
                CreateMetadata());

        Assert.Null(
            result.Filter);
    }

    [Fact]
    public void Compile_ShouldCompileFilterCondition()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        nameof(TestRecord.Name),
                        FilterOperator.Equals,
                        "Andrew")));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        var condition =
            Assert.IsType<CompiledFilterCondition>(
                result.Filter);

        Assert.Equal(
            nameof(TestRecord.Name),
            condition.Field.Name);

        Assert.Equal(
            FilterOperator.Equals,
            condition.Operator);

        Assert.Single(
            condition.Values);

        Assert.Equal(
            "Andrew",
            condition.Values[0]);
    }

    [Fact]
    public void Compile_ShouldCompileNestedFilterGroup()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateGroup(
                        LogicalOperator.And,
                        QueryFilterNode.CreateCondition(
                            nameof(TestRecord.Name),
                            FilterOperator.Equals,
                            "A"),
                        QueryFilterNode.CreateGroup(
                            LogicalOperator.Or,
                            QueryFilterNode.CreateCondition(
                                nameof(TestRecord.Category),
                                FilterOperator.Equals,
                                "One"),
                            QueryFilterNode.CreateCondition(
                                nameof(TestRecord.Category),
                                FilterOperator.Equals,
                                "Two")))));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        var group =
            Assert.IsType<CompiledFilterGroup>(
                result.Filter);

        Assert.Equal(
            LogicalOperator.And,
            group.Operator);

        Assert.Equal(
            2,
            group.Filters.Count);

        Assert.IsType<CompiledFilterCondition>(
            group.Filters[0]);

        Assert.IsType<CompiledFilterGroup>(
            group.Filters[1]);
    }

    [Fact]
    public void Compile_ShouldThrow_WhenFilterFieldDoesNotExist()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: QueryFilterNode.CreateCondition(
                        "MissingField",
                        FilterOperator.Equals,
                        "A")));

        Assert.Throws<InvalidOperationException>(
            () => _compiler.Compile(
                request,
                CreateMetadata()));
    }

    [Fact]
    public void Compile_ShouldThrow_WhenFilterNodeContainsConditionAndGroup()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: new QueryFilterNode(
                        new QueryFilterCondition(
                            nameof(TestRecord.Name),
                            FilterOperator.Equals,
                            ["A"]),
                        new QueryFilterGroup(
                            LogicalOperator.And,
                            []))));

        Assert.Throws<InvalidOperationException>(
            () => _compiler.Compile(
                request,
                CreateMetadata()));
    }

    [Fact]
    public void Compile_ShouldThrow_WhenFilterNodeContainsNeitherConditionNorGroup()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter: new QueryFilterNode(
                        null,
                        null)));

        Assert.Throws<InvalidOperationException>(
            () => _compiler.Compile(
                request,
                CreateMetadata()));
    }

    [Fact]
    public void Compile_ShouldReturnNullSearch_WhenRequestDoesNotHaveSearch()
    {
        var result =
            _compiler.Compile(
                new QueryRequest(),
                CreateMetadata());

        Assert.Null(
            result.Search);
    }

    [Fact]
    public void Compile_ShouldCompileSearchCondition_ForSpecificField()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search: QuerySearchNode.CreateCondition(
                        "abc",
                        MatchMode.Contains,
                        nameof(TestRecord.Name))));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        var condition =
            Assert.IsType<CompiledSearchCondition>(
                result.Search);

        Assert.Equal(
            nameof(TestRecord.Name),
            condition.Field.Name);

        Assert.Equal(
            "abc",
            condition.SearchText);

        Assert.Equal(
            MatchMode.Contains,
            condition.MatchMode);
    }

    [Fact]
    public void Compile_ShouldCompileSearchAcrossMatchingFields_AsOrGroup()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search: QuerySearchNode.CreateCondition(
                        "abc",
                        MatchMode.Contains)));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        var group =
            Assert.IsType<CompiledSearchGroup>(
                result.Search);

        Assert.Equal(
            LogicalOperator.Or,
            group.Operator);

        Assert.Equal(
            2,
            group.Searches.Count);

        var first =
            Assert.IsType<CompiledSearchCondition>(
                group.Searches[0]);

        var second =
            Assert.IsType<CompiledSearchCondition>(
                group.Searches[1]);

        Assert.Equal(
            nameof(TestRecord.Name),
            first.Field.Name);

        Assert.Equal(
            nameof(TestRecord.Category),
            second.Field.Name);
    }

    [Fact]
    public void Compile_ShouldOrderSearchConditions_BySearchPriority()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search: QuerySearchNode.CreateCondition(
                        "abc",
                        MatchMode.Contains)));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        var group =
            Assert.IsType<CompiledSearchGroup>(
                result.Search);

        var first =
            Assert.IsType<CompiledSearchCondition>(
                group.Searches[0]);

        var second =
            Assert.IsType<CompiledSearchCondition>(
                group.Searches[1]);

        Assert.Equal(
            nameof(TestRecord.Name),
            first.Field.Name);

        Assert.Equal(
            nameof(TestRecord.Category),
            second.Field.Name);
    }

    [Fact]
    public void Compile_ShouldCompileNestedSearchGroup()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search: QuerySearchNode.CreateGroup(
                        LogicalOperator.And,
                        QuerySearchNode.CreateCondition(
                            "abc",
                            MatchMode.Contains,
                            nameof(TestRecord.Name)),
                        QuerySearchNode.CreateGroup(
                            LogicalOperator.Or,
                            QuerySearchNode.CreateCondition(
                                "one",
                                MatchMode.Contains,
                                nameof(TestRecord.Category)),
                            QuerySearchNode.CreateCondition(
                                "two",
                                MatchMode.Contains,
                                nameof(TestRecord.Category))))));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        var group =
            Assert.IsType<CompiledSearchGroup>(
                result.Search);

        Assert.Equal(
            LogicalOperator.And,
            group.Operator);

        Assert.Equal(
            2,
            group.Searches.Count);

        Assert.IsType<CompiledSearchCondition>(
            group.Searches[0]);

        Assert.IsType<CompiledSearchGroup>(
            group.Searches[1]);
    }

    [Fact]
    public void Compile_ShouldThrow_WhenSearchNodeContainsConditionAndGroup()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search: new QuerySearchNode(
                        new QuerySearchCondition(
                            "abc",
                            MatchMode.Contains,
                            nameof(TestRecord.Name)),
                        new QuerySearchGroup(
                            LogicalOperator.And,
                            []))));

        Assert.Throws<InvalidOperationException>(
            () => _compiler.Compile(
                request,
                CreateMetadata()));
    }

    [Fact]
    public void Compile_ShouldThrow_WhenSearchNodeContainsNeitherConditionNorGroup()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search: new QuerySearchNode(
                        null,
                        null)));

        Assert.Throws<InvalidOperationException>(
            () => _compiler.Compile(
                request,
                CreateMetadata()));
    }

    [Fact]
    public void Compile_ShouldReturnEmptySorts_WhenSortIsNull()
    {
        var result =
            _compiler.Compile(
                new QueryRequest(),
                CreateMetadata());

        Assert.Empty(
            result.Sort);
    }

    [Fact]
    public void Compile_ShouldReturnEmptySorts_WhenSortIsEmpty()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Sort: []));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        Assert.Empty(
            result.Sort);
    }

    [Fact]
    public void Compile_ShouldCompileSorts_BySequence()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            nameof(TestRecord.Amount),
                            SortDirection.Descending,
                            Sequence: 2),

                        new QuerySort(
                            nameof(TestRecord.Name),
                            SortDirection.Ascending,
                            Sequence: 1)
                    ]));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        Assert.Equal(
            2,
            result.Sort.Count);

        Assert.Equal(
            nameof(TestRecord.Name),
            result.Sort[0].Field.Name);

        Assert.Equal(
            SortDirection.Ascending,
            result.Sort[0].Direction);

        Assert.Equal(
            0,
            result.Sort[0].Sequence);

        Assert.Equal(
            nameof(TestRecord.Amount),
            result.Sort[1].Field.Name);

        Assert.Equal(
            SortDirection.Descending,
            result.Sort[1].Direction);

        Assert.Equal(
            1,
            result.Sort[1].Sequence);
    }

    [Fact]
    public void Compile_ShouldCompileSortsWithoutSequence_AfterSequencedSorts()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            nameof(TestRecord.Amount),
                            SortDirection.Descending),

                        new QuerySort(
                            nameof(TestRecord.Name),
                            SortDirection.Ascending,
                            Sequence: 1)
                    ]));

        var result =
            _compiler.Compile(
                request,
                CreateMetadata());

        Assert.Equal(
            2,
            result.Sort.Count);

        Assert.Equal(
            nameof(TestRecord.Name),
            result.Sort[0].Field.Name);

        Assert.Equal(
            nameof(TestRecord.Amount),
            result.Sort[1].Field.Name);
    }

    [Fact]
    public void Compile_ShouldThrow_WhenSortFieldDoesNotExist()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            "MissingField",
                            SortDirection.Ascending)
                    ]));

        Assert.Throws<InvalidOperationException>(
            () => _compiler.Compile(
                request,
                CreateMetadata()));
    }

    private static RecordMetadata CreateMetadata()
    {
        return new RecordMetadata(
            "test-record",
            "Test Record",
            "1.0.0",
            "Unit Test",
            [
                new FieldMetadata(
                    nameof(TestRecord.Name),
                    typeof(string),
                    true,
                    [FilterOperator.Equals],
                    true,
                    1,
                    [MatchMode.Exact, MatchMode.Contains],
                    true),

                new FieldMetadata(
                    nameof(TestRecord.Category),
                    typeof(string),
                    true,
                    [FilterOperator.Equals],
                    true,
                    2,
                    [MatchMode.Contains],
                    false),

                new FieldMetadata(
                    nameof(TestRecord.Amount),
                    typeof(decimal),
                    true,
                    [
                        FilterOperator.Equals,
                        FilterOperator.GreaterThan,
                        FilterOperator.GreaterThanOrEqual
                    ],
                    false,
                    null,
                    [],
                    true)
            ],
            new PageableMetadata(
                25,
                100));
    }

    private static RecordMetadata CreateMetadataWithoutPageable()
    {
        return new RecordMetadata(
            "test-record",
            "Test Record",
            "1.0.0",
            "Unit Test",
            [
                new FieldMetadata(
                    nameof(TestRecord.Name),
                    typeof(string),
                    true,
                    [FilterOperator.Equals],
                    true,
                    1,
                    [MatchMode.Exact, MatchMode.Contains],
                    true)
            ],
            null);
    }

    private sealed record TestRecord(
        string Name,
        string Category,
        decimal Amount);
}
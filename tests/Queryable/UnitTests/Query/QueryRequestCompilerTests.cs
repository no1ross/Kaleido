using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class QueryRequestCompilerTests
{
    private readonly QueryRequestCompiler _compiler = new();

    [Fact]
    public void Compile_WhenRequestIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _compiler.Compile(null!, CreateContextMetadata()));
    }

    [Fact]
    public void Compile_WhenMetadataIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _compiler.Compile(new QueryRequest(), null!));
    }

    [Fact]
    public void Compile_UsesContextPageableDefaults()
    {
        var result = _compiler.Compile(new QueryRequest(), CreateContextMetadata());

        Assert.Equal(25, result.Page.Size);
        Assert.Equal(0, result.Page.Offset);
    }

    [Fact]
    public void Compile_UsesViewPageableDefaults()
    {
        var result = _compiler.Compile(new QueryRequest(), CreateContextMetadataWithoutPageable(), CreateViewMetadata());

        Assert.Equal(10, result.Page.Size);
        Assert.Equal(0, result.Page.Offset);
    }

    [Fact]
    public void Compile_UsesFallbackPageSizeWhenNoPageableIsDefined()
    {
        var result = _compiler.Compile(new QueryRequest(), CreateContextMetadataWithoutPageable());

        Assert.Equal(50, result.Page.Size);
        Assert.Equal(0, result.Page.Offset);
    }

    [Fact]
    public void Compile_ClampsRequestedPageSizeToMaxSize()
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(500, 3)));

        var result = _compiler.Compile(request, CreateContextMetadata());

        Assert.Equal(100, result.Page.Size);
        Assert.Equal(3, result.Page.Offset);
    }

    [Fact]
    public void Compile_CompilesFilterCondition()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition(nameof(TestRecord.Code), FilterOperator.Equals, "A")));

        var result = _compiler.Compile(request, CreateContextMetadata());

        var condition = Assert.IsType<CompiledFilterCondition>(result.Filter);
        Assert.Equal(nameof(TestRecord.Code), condition.Field.Name);
        Assert.Equal(FilterOperator.Equals, condition.Operator);
        Assert.Equal("A", condition.Values.Single());
    }

    [Fact]
    public void Compile_CompilesNestedFilterGroup()
    {
        var request = new QueryRequest(
            new QueryBody(
                Filter: QueryFilterNode.CreateGroup(
                    LogicalOperator.And,
                    QueryFilterNode.CreateCondition(nameof(TestRecord.Code), FilterOperator.Equals, "A"),
                    QueryFilterNode.CreateGroup(
                        LogicalOperator.Or,
                        QueryFilterNode.CreateCondition(nameof(TestRecord.Name), FilterOperator.Contains, "A")))));

        var result = _compiler.Compile(request, CreateContextMetadata());

        var group = Assert.IsType<CompiledFilterGroup>(result.Filter);
        Assert.Equal(LogicalOperator.And, group.Operator);
        Assert.Equal(2, group.Filters.Count);
    }

    [Fact]
    public void Compile_CompilesSearchFieldsByPriority()
    {
        var request = new QueryRequest(new QueryBody(SearchText: "abc"));

        var result = _compiler.Compile(request, CreateContextMetadata());

        var search = Assert.IsType<CompiledSearch>(result.Search);
        Assert.Equal("abc", search.SearchText);
        Assert.Equal([nameof(TestRecord.Name), nameof(TestRecord.Region)], search.Fields.Select(x => x.Field.Name));
    }

    [Fact]
    public void Compile_CompilesSortsBySequence()
    {
        var request = new QueryRequest(
            new QueryBody(
                Sort:
                [
                    new QuerySort(nameof(TestRecord.Region), SortDirection.Descending, 2),
                    new QuerySort(nameof(TestRecord.Code), SortDirection.Ascending, 1)
                ]));

        var result = _compiler.Compile(request, CreateContextMetadata());

        Assert.Equal([nameof(TestRecord.Code), nameof(TestRecord.Region)], result.Sort.Select(x => x.Field.Name));
        Assert.Equal([0, 1], result.Sort.Select(x => x.Sequence));
    }

    [Fact]
    public void Compile_WhenFieldIsMissing_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Missing", FilterOperator.Equals, "A")));

        var exception = Assert.Throws<InvalidOperationException>(() => _compiler.Compile(request, CreateContextMetadata()));

        Assert.Contains("Field 'Missing' is not defined", exception.Message);
    }

    private static QueryContextMetadata CreateContextMetadata() =>
        new(
            "test-record",
            "Test Record",
            "Test Record",
            "1.0.0",
            "Unit Test",
            true,
            new PageableMetadata(25, 100),
            [
                new FieldMetadata(nameof(TestRecord.Code), null, typeof(string), true, [FilterOperator.Equals], false, null, null, true),
                new FieldMetadata(nameof(TestRecord.Name), null, typeof(string), false, [], true, 1, MatchMode.Contains, false),
                new FieldMetadata(nameof(TestRecord.Region), null, typeof(string), false, [], true, 2, MatchMode.Contains, true)
            ]);

    private static QueryContextMetadata CreateContextMetadataWithoutPageable() =>
        CreateContextMetadata() with { Pageable = null };

    private static QueryViewMetadata CreateViewMetadata() =>
        new(
            "test-view",
            "1.0.0",
            "Test View",
            "Test View",
            new PageableMetadata(10, 20),
            []);

    private sealed class TestRecord
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
    }
}

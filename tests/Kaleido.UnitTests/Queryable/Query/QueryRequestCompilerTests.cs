using Kaleido.Exceptions;
using Kaleido.Registry;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryRequestCompilerTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly DataTypeDescriptor TestDataType =
        new("string");

    private QueryRequestCompiler Sut =>
        CreateSut();

    private static QueryRequestCompiler CreateSut() =>
        new();

    [Fact]
    public void Compile_UsesContextPageableDefaults()
    {
        var result = Compile(new QueryRequest(), CreateContextMetadata());

        Assert.Equal(25, result.Page.Size);
        Assert.Equal(0, result.Page.Offset);
    }

    [Fact]
    public void Compile_UsesViewPageableDefaults()
    {
        var result = Sut.Compile(new QueryRequest(), CreateContextMetadataWithoutPageable(), CreateViewMetadata().Pageable);

        Assert.Equal(10, result.Page.Size);
        Assert.Equal(0, result.Page.Offset);
    }

    [Fact]
    public void Compile_UsesFallbackPageSizeWhenNoPageableIsDefined()
    {
        var result = Compile(new QueryRequest(), CreateContextMetadataWithoutPageable());

        Assert.Equal(50, result.Page.Size);
        Assert.Equal(0, result.Page.Offset);
    }

    [Fact]
    public void Compile_PageWithoutSize_UsesSourceDefaultSize()
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(null, 20)));

        var result = Compile(request, CreateContextMetadata());

        Assert.Equal(25, result.Page.Size);
        Assert.Equal(20, result.Page.Offset);
    }

    [Fact]
    public void Compile_PageWithoutSize_UsesViewDefaultSize()
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(null, null)));

        var result = Sut.Compile(request, CreateContextMetadata(), CreateViewMetadata().Pageable);

        Assert.Equal(10, result.Page.Size);
        Assert.Equal(0, result.Page.Offset);
    }

    [Fact]
    public void Compile_ClampsRequestedPageSizeToMaxSize()
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(500, 3)));

        var result = Compile(request, CreateContextMetadata());

        Assert.Equal(100, result.Page.Size);
        Assert.Equal(3, result.Page.Offset);
    }

    [Fact]
    public void Compile_CompilesFilterCondition()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition(nameof(TestRecord.Code), FilterOperator.Equals, "A")));

        var result = Compile(request, CreateContextMetadata());

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

        var result = Compile(request, CreateContextMetadata());

        var group = Assert.IsType<CompiledFilterGroup>(result.Filter);
        Assert.Equal(LogicalOperator.And, group.Operator);
        Assert.Equal(2, group.Filters.Count);
    }

    [Fact]
    public void Compile_CompilesSearchFieldsByPriority()
    {
        var request = new QueryRequest(new QueryBody(SearchText: "abc"));

        var result = Compile(request, CreateContextMetadata());

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

        var result = Compile(request, CreateContextMetadata());

        Assert.Equal([nameof(TestRecord.Code), nameof(TestRecord.Region)], result.Sort.Select(x => x.Field.Name));
        Assert.Equal([0, 1], result.Sort.Select(x => x.Sequence));
    }

    [Fact]
    public void Compile_WithoutSort_UsesTheDefaultSortField_Ascending()
    {
        var result = Sut.Compile(new QueryRequest(), CreateContextMetadata(), new PageableMetadata(10, 20, nameof(TestRecord.Region)));

        var sort = Assert.Single(result.Sort);
        Assert.Equal(nameof(TestRecord.Region), sort.Field.Name);
        Assert.Equal(SortDirection.Ascending, sort.Direction);
    }

    [Fact]
    public void Compile_TheRequestSort_WinsOverTheDefault()
    {
        var request = new QueryRequest(new QueryBody(Sort: [new QuerySort(nameof(TestRecord.Code), SortDirection.Descending, 1)]));

        var result = Sut.Compile(request, CreateContextMetadata(), new PageableMetadata(10, 20, nameof(TestRecord.Region)));

        var sort = Assert.Single(result.Sort);
        Assert.Equal(nameof(TestRecord.Code), sort.Field.Name);
        Assert.Equal(SortDirection.Descending, sort.Direction);
    }

    [Fact]
    public void Compile_WithoutSortOrDefault_LeavesTheQueryUnsorted()
    {
        Assert.Empty(Compile(new QueryRequest(), CreateContextMetadata()).Sort);
        Assert.Empty(Compile(new QueryRequest(), CreateContextMetadataWithoutPageable()).Sort);
    }

    [Fact]
    public void Compile_WhenFieldIsMissing_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Missing", FilterOperator.Equals, "A")));

        var exception = Assert.Throws<KaleidoValidationException>(() => Compile(request, CreateContextMetadata()));

        Assert.Equal(QueryableErrorCodes.InvalidField, exception.Code);
        Assert.Contains("Field 'Missing' does not exist", exception.Message);
    }

    [Fact]
    public void Compile_WhenRequestOrMetadataIsNull_Throws()
    {
        Assert.Equal("request", Assert.Throws<ArgumentNullException>(() => Sut.Compile(null!, CreateContextMetadata(), null)).ParamName);
        Assert.Equal("metadata", Assert.Throws<ArgumentNullException>(() => Sut.Compile(new QueryRequest(), null!, null)).ParamName);
    }

    [Fact]
    public void Compile_WhenANodeHasConditionAndGroup_Throws()
    {
        var node = new QueryFilterNode(
            new QueryFilterCondition(nameof(TestRecord.Code), FilterOperator.Equals, ["A"]),
            new QueryFilterGroup(LogicalOperator.And, []));

        var exception = Assert.Throws<KaleidoValidationException>(() => Compile(new QueryRequest(new QueryBody(Filter: node)), CreateContextMetadata()));

        Assert.Equal(QueryableErrorCodes.InvalidFilterNode, exception.Code);
    }

    [Fact]
    public void Compile_WhenANodeHasNeitherConditionNorGroup_Throws()
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Compile(new QueryRequest(new QueryBody(Filter: new QueryFilterNode(null, null))), CreateContextMetadata()));

        Assert.Equal(QueryableErrorCodes.InvalidFilterNode, exception.Code);
    }

    [Fact]
    public void Compile_WhenASearchableFieldHasNoMatchMode_ThrowsFrameworkException()
    {
        var metadata = CreateContextMetadata() with
        {
            Fields = [new FieldMetadata { Name = nameof(TestRecord.Name), FieldType = typeof(string), DataType = TestDataType, IsSearchable = true, SearchPriority = 1, MatchMode = null }],
        };

        var exception = Assert.Throws<KaleidoFrameworkException>(() => Compile(new QueryRequest(new QueryBody(SearchText: "abc")), metadata));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
        Assert.Contains(nameof(TestRecord.Name), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_SearchFieldWithoutPriority_IsOrderedLast()
    {
        var metadata = CreateContextMetadata() with
        {
            Fields =
            [
                new FieldMetadata { Name = nameof(TestRecord.Code), FieldType = typeof(string), DataType = TestDataType, IsSearchable = true, SearchPriority = null, MatchMode = MatchMode.Exact },
                new FieldMetadata { Name = nameof(TestRecord.Name), FieldType = typeof(string), DataType = TestDataType, IsSearchable = true, SearchPriority = 3, MatchMode = MatchMode.Contains },
            ],
        };

        var search = Assert.IsType<CompiledSearch>(Compile(new QueryRequest(new QueryBody(SearchText: "abc")), metadata).Search);

        Assert.Equal([nameof(TestRecord.Name), nameof(TestRecord.Code)], search.Fields.Select(x => x.Field.Name));
        Assert.Equal([3, int.MaxValue], search.Fields.Select(x => x.Priority));
    }

    [Fact]
    public void Compile_SortsWithoutSequence_KeepTheirOrderAfterSequencedSorts()
    {
        var request = new QueryRequest(
            new QueryBody(
                Sort:
                [
                    new QuerySort(nameof(TestRecord.Region), SortDirection.Descending),
                    new QuerySort(nameof(TestRecord.Code), SortDirection.Ascending, 1),
                ]));

        var result = Compile(request, CreateContextMetadata());

        Assert.Equal([nameof(TestRecord.Code), nameof(TestRecord.Region)], result.Sort.Select(x => x.Field.Name));
    }

    [Fact]
    public void Compile_EmptySortList_UsesTheDefaultSortField()
    {
        var result = Sut.Compile(new QueryRequest(new QueryBody(Sort: [])), CreateContextMetadata(), new PageableMetadata(10, 20, nameof(TestRecord.Code)));

        Assert.Equal(nameof(TestRecord.Code), Assert.Single(result.Sort).Field.Name);
    }

    private CompiledQuery Compile(IQueryRequest request, QuerySourceMetadata metadata) =>
        Sut.Compile(request, metadata, metadata.Pageable);

    private static QuerySourceMetadata CreateContextMetadata() =>
        new(
            "TestSource",
            "Test Record",
            "Test Record",
            "1.0.0",
            "Unit Test",
            QuerySourceKind.Local,
            new PageableMetadata(25, 100),
            [
                new FieldMetadata { Name = nameof(TestRecord.Code), Description = null, FieldType = typeof(string), DataType = TestDataType, IsFilterable = true, FilterOperators = [FilterOperator.Equals], IsSearchable = false, SearchPriority = null, MatchMode = null, IsSortable = true },
                new FieldMetadata { Name = nameof(TestRecord.Name), Description = null, FieldType = typeof(string), DataType = TestDataType, IsFilterable = false, FilterOperators = [], IsSearchable = true, SearchPriority = 1, MatchMode = MatchMode.Contains, IsSortable = false },
                new FieldMetadata { Name = nameof(TestRecord.Region), Description = null, FieldType = typeof(string), DataType = TestDataType, IsFilterable = false, FilterOperators = [], IsSearchable = true, SearchPriority = 2, MatchMode = MatchMode.Contains, IsSortable = true }
            ],
            [],
            [],
            AuthorizationMetadata.Unspecified);

    private static QuerySourceMetadata CreateContextMetadataWithoutPageable() =>
        CreateContextMetadata() with { Pageable = null };

    private static QueryViewMetadata CreateViewMetadata() =>
        new(
            "test-view",
            "1.0.0",
            "Test View",
            "Test View",
            new PageableMetadata(10, 20),
            [],
            []);

    private sealed class TestRecord
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Region { get; init; } = string.Empty;
    }
}

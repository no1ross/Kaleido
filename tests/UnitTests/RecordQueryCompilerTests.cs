using Kaleido.Metadata;
using Xunit;

namespace Kaleido.UnitTests;

public sealed class RecordQueryCompilerTests
{
    private readonly RecordQueryCompiler _sut = new();

    [Fact]
    public void Compile_Should_Throw_When_Request_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => _sut.Compile(null!, metadata));

        // Assert
        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void Compile_Should_Throw_When_Metadata_Is_Null()
    {
        // Arrange
        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: null,
            Parameters: null);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => _sut.Compile(request, null!));

        // Assert
        Assert.Equal("metadata", exception.ParamName);
    }

    [Fact]
    public void Compile_Should_Copy_QueryName_And_Parameters()
    {
        // Arrange
        var metadata = CreateMetadata();

        var parameters = new Dictionary<string, object?>
        {
            ["status"] = "Active"
        };

        var request = new KaleidoQueryRequest(
            QueryName: "by-status",
            Query: null,
            Parameters: parameters);

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Equal("by-status", result.NamedQuery);
        Assert.Same(parameters, result.Parameters);
    }

    [Fact]
    public void Compile_Should_Allow_Null_QueryName()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: null,
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Null(result.NamedQuery);
    }

    [Fact]
    public void Compile_Should_Use_Request_Page_When_Provided()
    {
        // Arrange
        var metadata = CreateMetadata(
            defaultPageSize: 25,
            maxPageSize: 100);

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: new QueryPage(
                    Size: 10,
                    Offset: 40)),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Equal(10, result.Page.Size);
        Assert.Equal(40, result.Page.Offset);
    }

    [Fact]
    public void Compile_Should_Use_Metadata_Default_Page_Size_When_Request_Size_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata(
            defaultPageSize: 25,
            maxPageSize: 100);

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: new QueryPage(
                    Size: null,
                    Offset: 15)),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Equal(25, result.Page.Size);
        Assert.Equal(15, result.Page.Offset);
    }

    [Fact]
    public void Compile_Should_Use_Default_Page_Size_Of_50_When_Request_And_Metadata_Do_Not_Provide_Size()
    {
        // Arrange
        var metadata = CreateMetadata(
            supportsPaging: false);

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: null,
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Equal(50, result.Page.Size);
        Assert.Equal(0, result.Page.Offset);
    }

    [Fact]
    public void Compile_Should_Use_Default_Offset_Of_0_When_Request_Offset_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: new QueryPage(
                    Size: 20,
                    Offset: null)),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Equal(20, result.Page.Size);
        Assert.Equal(0, result.Page.Offset);
    }

    [Fact]
    public void Compile_Should_Clamp_Page_Size_To_MaxSize()
    {
        // Arrange
        var metadata = CreateMetadata(
            defaultPageSize: 50,
            maxPageSize: 100);

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: new QueryPage(
                    Size: 500,
                    Offset: 0)),
            Parameters: null);

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Equal(100, result.Page.Size);
    }

    [Fact]
    public void Compile_Should_Clamp_Default_Page_Size_To_MaxSize()
    {
        // Arrange
        var metadata = CreateMetadata(
            defaultPageSize: 500,
            maxPageSize: 100);

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: null,
            Parameters: null);

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Equal(100, result.Page.Size);
    }

    [Fact]
    public void Compile_Should_Return_Null_Filter_When_Request_Has_No_Filter()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Null(result.Filter);
    }

    [Fact]
    public void Compile_Should_Compile_Filter_Condition()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: FilterCondition(
                    field: "Code",
                    @operator: FilterOperator.Eq,
                    "ABC"),
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var condition = Assert.IsType<CompiledFilterCondition>(result.Filter);

        Assert.Equal("Code", condition.Field.Name);
        Assert.Equal(FilterOperator.Eq, condition.Operator);
        Assert.Equal(new List<object?> { "ABC" }, condition.Values);
    }

    [Fact]
    public void Compile_Should_Resolve_Filter_Field_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: FilterCondition(
                    field: "code",
                    @operator: FilterOperator.Eq,
                    "ABC"),
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var condition = Assert.IsType<CompiledFilterCondition>(result.Filter);

        Assert.Equal("Code", condition.Field.Name);
    }

    [Fact]
    public void Compile_Should_Compile_Filter_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var filter = FilterGroup(
            LogicalOperator.And,
            FilterCondition(
                field: "Code",
                @operator: FilterOperator.Eq,
                "ABC"),
            FilterCondition(
                field: "Name",
                @operator: FilterOperator.Contains,
                "Test"));

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: filter,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var group = Assert.IsType<CompiledFilterGroup>(result.Filter);

        Assert.Equal(LogicalOperator.And, group.Operator);
        Assert.Equal(2, group.Expressions.Count);

        Assert.All(group.Expressions, expression =>
            Assert.IsType<CompiledFilterCondition>(expression));
    }

    [Fact]
    public void Compile_Should_Compile_Nested_Filter_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var filter = FilterGroup(
            LogicalOperator.And,
            FilterCondition(
                field: "Code",
                @operator: FilterOperator.Eq,
                "ABC"),
            FilterGroup(
                LogicalOperator.Or,
                FilterCondition(
                    field: "Name",
                    @operator: FilterOperator.Contains,
                    "Test"),
                FilterCondition(
                    field: "Name",
                    @operator: FilterOperator.Eq,
                    "Other")));

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: filter,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var root = Assert.IsType<CompiledFilterGroup>(result.Filter);

        Assert.Equal(LogicalOperator.And, root.Operator);
        Assert.Equal(2, root.Expressions.Count);

        Assert.IsType<CompiledFilterCondition>(root.Expressions[0]);

        var nested = Assert.IsType<CompiledFilterGroup>(root.Expressions[1]);

        Assert.Equal(LogicalOperator.Or, nested.Operator);
        Assert.Equal(2, nested.Expressions.Count);
    }

    [Fact]
    public void Compile_Should_Throw_When_Filter_Node_Has_Both_Condition_And_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var filter = new QueryFilterNode(
            Condition: new QueryFilterCondition(
                Field: "Code",
                Operator: FilterOperator.Eq,
                Values: new List<object?> { "ABC" }),
            Group: new QueryFilterGroup(
                Operator: LogicalOperator.And,
                Filters: new List<QueryFilterNode>
                {
                    FilterCondition(
                        field: "Name",
                        @operator: FilterOperator.Contains,
                        "Test")
                }));

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: filter,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Compile(request, metadata));

        // Assert
        Assert.Equal(
            "Filter node cannot specify both Condition and Group.",
            exception.Message);
    }

    [Fact]
    public void Compile_Should_Throw_When_Filter_Node_Has_Neither_Condition_Nor_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: new QueryFilterNode(
                    Condition: null,
                    Group: null),
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Compile(request, metadata));

        // Assert
        Assert.Equal(
            "Filter node must specify either Condition or Group.",
            exception.Message);
    }

    [Fact]
    public void Compile_Should_Throw_When_Filter_Field_Does_Not_Exist()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: FilterCondition(
                    field: "MissingField",
                    @operator: FilterOperator.Eq,
                    "ABC"),
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Compile(request, metadata));

        // Assert
        Assert.Equal(
            "Field 'MissingField' is not defined for record 'TestRecord'.",
            exception.Message);
    }

    [Fact]
    public void Compile_Should_Return_Null_Search_When_Request_Has_No_Search()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Null(result.Search);
    }

    [Fact]
    public void Compile_Should_Compile_Search_For_Specific_Field()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: SearchCondition(
                    searchText: "abc",
                    matchMode: MatchMode.Contains,
                    field: "Name"),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var condition = Assert.IsType<CompiledSearchCondition>(result.Search);

        Assert.Equal("Name", condition.Field.Name);
        Assert.Equal("abc", condition.SearchText);
        Assert.Equal(MatchMode.Contains, condition.MatchMode);
    }

    [Fact]
    public void Compile_Should_Resolve_Search_Field_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: SearchCondition(
                    searchText: "abc",
                    matchMode: MatchMode.Contains,
                    field: "name"),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var condition = Assert.IsType<CompiledSearchCondition>(result.Search);

        Assert.Equal("Name", condition.Field.Name);
    }

    [Fact]
    public void Compile_Should_Compile_Search_Across_All_Searchable_Fields_When_Field_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: SearchCondition(
                    searchText: "abc",
                    matchMode: MatchMode.Contains,
                    field: null),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var group = Assert.IsType<CompiledSearchGroup>(result.Search);

        Assert.Equal(LogicalOperator.Or, group.Operator);
        Assert.Equal(2, group.Expressions.Count);

        var first = Assert.IsType<CompiledSearchCondition>(group.Expressions[0]);
        var second = Assert.IsType<CompiledSearchCondition>(group.Expressions[1]);

        Assert.Equal("Code", first.Field.Name);
        Assert.Equal("Name", second.Field.Name);
    }

    [Fact]
    public void Compile_Should_Order_Search_Fields_By_SearchPriority()
    {
        // Arrange
        var metadata = CreateMetadataWithSearchPriorities(
            codePriority: 2,
            namePriority: 1);

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: SearchCondition(
                    searchText: "abc",
                    matchMode: MatchMode.Contains,
                    field: null),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var group = Assert.IsType<CompiledSearchGroup>(result.Search);

        var first = Assert.IsType<CompiledSearchCondition>(group.Expressions[0]);
        var second = Assert.IsType<CompiledSearchCondition>(group.Expressions[1]);

        Assert.Equal("Name", first.Field.Name);
        Assert.Equal("Code", second.Field.Name);
    }

    [Fact]
    public void Compile_Should_Return_Empty_Search_Group_When_No_Searchable_Fields_Match()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: SearchCondition(
                    searchText: "abc",
                    matchMode: MatchMode.StartsWith,
                    field: null),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var group = Assert.IsType<CompiledSearchGroup>(result.Search);

        Assert.Equal(LogicalOperator.Or, group.Operator);
        Assert.Empty(group.Expressions);
    }

    [Fact]
    public void Compile_Should_Compile_Search_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var search = SearchGroup(
            LogicalOperator.And,
            SearchCondition(
                searchText: "abc",
                matchMode: MatchMode.Contains,
                field: "Code"),
            SearchCondition(
                searchText: "test",
                matchMode: MatchMode.Contains,
                field: "Name"));

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: search,
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var group = Assert.IsType<CompiledSearchGroup>(result.Search);

        Assert.Equal(LogicalOperator.And, group.Operator);
        Assert.Equal(2, group.Expressions.Count);

        Assert.All(group.Expressions, expression =>
            Assert.IsType<CompiledSearchCondition>(expression));
    }

    [Fact]
    public void Compile_Should_Compile_Nested_Search_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var search = SearchGroup(
            LogicalOperator.And,
            SearchCondition(
                searchText: "abc",
                matchMode: MatchMode.Contains,
                field: "Code"),
            SearchGroup(
                LogicalOperator.Or,
                SearchCondition(
                    searchText: "test",
                    matchMode: MatchMode.Contains,
                    field: "Name"),
                SearchCondition(
                    searchText: "other",
                    matchMode: MatchMode.Exact,
                    field: "Code")));

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: search,
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var root = Assert.IsType<CompiledSearchGroup>(result.Search);

        Assert.Equal(LogicalOperator.And, root.Operator);
        Assert.Equal(2, root.Expressions.Count);

        Assert.IsType<CompiledSearchCondition>(root.Expressions[0]);

        var nested = Assert.IsType<CompiledSearchGroup>(root.Expressions[1]);

        Assert.Equal(LogicalOperator.Or, nested.Operator);
        Assert.Equal(2, nested.Expressions.Count);
    }

    [Fact]
    public void Compile_Should_Throw_When_Search_Node_Has_Both_Condition_And_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var search = new QuerySearchNode(
            Condition: new QuerySearchCondition(
                SearchText: "abc",
                MatchMode: MatchMode.Contains,
                Field: "Code"),
            Group: new QuerySearchGroup(
                Operator: LogicalOperator.And,
                Searches: new List<QuerySearchNode>
                {
                    SearchCondition(
                        searchText: "test",
                        matchMode: MatchMode.Contains,
                        field: "Name")
                }));

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: search,
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Compile(request, metadata));

        // Assert
        Assert.Equal(
            "Search node cannot specify both Condition and Group.",
            exception.Message);
    }

    [Fact]
    public void Compile_Should_Throw_When_Search_Node_Has_Neither_Condition_Nor_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: new QuerySearchNode(
                    Condition: null,
                    Group: null),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Compile(request, metadata));

        // Assert
        Assert.Equal(
            "Search node must specify either Condition or Group.",
            exception.Message);
    }

    [Fact]
    public void Compile_Should_Return_Empty_Sorts_When_Request_Has_No_Sorts()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Empty(result.Sort);
    }

    [Fact]
    public void Compile_Should_Return_Empty_Sorts_When_Request_Has_Empty_Sort_List()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: new List<QuerySort>(),
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Empty(result.Sort);
    }

    [Fact]
    public void Compile_Should_Compile_Sorts_In_Sequence_Order()
    {
        // Arrange
        var metadata = CreateMetadata();

        var sorts = new List<QuerySort>
        {
            new QuerySort(
                Field: "Name",
                Direction: SortDirection.Descending,
                Sequence: 2),

            new QuerySort(
                Field: "Code",
                Direction: SortDirection.Ascending,
                Sequence: 1)
        };

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: sorts,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Equal(2, result.Sort.Count);

        Assert.Equal("Code", result.Sort[0].Field.Name);
        Assert.Equal(SortDirection.Ascending, result.Sort[0].Direction);
        Assert.Equal(0, result.Sort[0].Sequence);

        Assert.Equal("Name", result.Sort[1].Field.Name);
        Assert.Equal(SortDirection.Descending, result.Sort[1].Direction);
        Assert.Equal(1, result.Sort[1].Sequence);
    }

    [Fact]
    public void Compile_Should_Order_Sorts_With_Null_Sequence_Last()
    {
        // Arrange
        var metadata = CreateMetadata();

        var sorts = new List<QuerySort>
        {
            new QuerySort(
                Field: "Name",
                Direction: SortDirection.Descending,
                Sequence: null),

            new QuerySort(
                Field: "Code",
                Direction: SortDirection.Ascending,
                Sequence: 1)
        };

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: sorts,
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        Assert.Equal("Code", result.Sort[0].Field.Name);
        Assert.Equal("Name", result.Sort[1].Field.Name);
    }

    [Fact]
    public void Compile_Should_Resolve_Sort_Field_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "test",
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: new List<QuerySort>
                {
                    new QuerySort(
                        Field: "code",
                        Direction: SortDirection.Ascending,
                        Sequence: null)
                },
                Page: null),
            Parameters: EmptyParameters());

        // Act
        var result = _sut.Compile(request, metadata);

        // Assert
        var sort = Assert.Single(result.Sort);

        Assert.Equal("Code", sort.Field.Name);
    }

    private static QueryFilterNode FilterCondition(
        string field,
        FilterOperator @operator,
        params object?[] values)
    {
        return new QueryFilterNode(
            Condition: new QueryFilterCondition(
                Field: field,
                Operator: @operator,
                Values: values.ToList()),
            Group: null);
    }

    private static QueryFilterNode FilterGroup(
        LogicalOperator @operator,
        params QueryFilterNode[] filters)
    {
        return new QueryFilterNode(
            Condition: null,
            Group: new QueryFilterGroup(
                Operator: @operator,
                Filters: filters.ToList()));
    }

    private static QuerySearchNode SearchCondition(
        string searchText,
        MatchMode matchMode,
        string? field = null)
    {
        return new QuerySearchNode(
            Condition: new QuerySearchCondition(
                SearchText: searchText,
                MatchMode: matchMode,
                Field: field),
            Group: null);
    }

    private static QuerySearchNode SearchGroup(
        LogicalOperator @operator,
        params QuerySearchNode[] searches)
    {
        return new QuerySearchNode(
            Condition: null,
            Group: new QuerySearchGroup(
                Operator: @operator,
                Searches: searches.ToList()));
    }

    private static RuntimeRecordMetadata CreateMetadata(
        int defaultPageSize = 50,
        int maxPageSize = 500,
        RuntimePageableMetadata? pageable = null,
        bool supportsPaging = true)
    {
        return new RuntimeRecordMetadata(
            Name: "TestRecord",
            Version: "1.0.0",
            Source: "Unit Test",
            Fields: new List<RuntimeFieldMetadata>
            {
                CreateField(
                    name: "Code",
                    fieldType: typeof(string),
                    isFilterable: true,
                    filterOperators: new List<FilterOperator>
                    {
                        FilterOperator.Eq,
                        FilterOperator.Contains
                    },
                    isSearchable: true,
                    searchPriority: 1,
                    matchModes: new List<MatchMode>
                    {
                        MatchMode.Contains,
                        MatchMode.Exact
                    },
                    isSortable: true),

                CreateField(
                    name: "Name",
                    fieldType: typeof(string),
                    isFilterable: true,
                    filterOperators: new List<FilterOperator>
                    {
                        FilterOperator.Eq,
                        FilterOperator.Contains
                    },
                    isSearchable: true,
                    searchPriority: 2,
                    matchModes: new List<MatchMode>
                    {
                        MatchMode.Contains,
                        MatchMode.Exact
                    },
                    isSortable: true),

                CreateField(
                    name: "Description",
                    fieldType: typeof(string),
                    isFilterable: false,
                    filterOperators: new List<FilterOperator>(),
                    isSearchable: false,
                    searchPriority: null,
                    matchModes: new List<MatchMode>(),
                    isSortable: false)
            },
            AllowedQueries: new List<RuntimeAllowedQueryMetadata>(),
            Pageable: supportsPaging
                ? pageable ?? new RuntimePageableMetadata(
                    DefaultSize: defaultPageSize,
                    MaxSize: maxPageSize)
                : null);
    }

    private static RuntimeRecordMetadata CreateMetadataWithSearchPriorities(
        int? codePriority,
        int? namePriority)
    {
        return new RuntimeRecordMetadata(
            Name: "TestRecord",
            Version: "1.0.0",
            Source: "Unit Test",
            Fields: new List<RuntimeFieldMetadata>
            {
                CreateField(
                    name: "Code",
                    fieldType: typeof(string),
                    isFilterable: true,
                    filterOperators: new List<FilterOperator>
                    {
                        FilterOperator.Eq,
                        FilterOperator.Contains
                    },
                    isSearchable: true,
                    searchPriority: codePriority,
                    matchModes: new List<MatchMode>
                    {
                        MatchMode.Contains,
                        MatchMode.Exact
                    },
                    isSortable: true),

                CreateField(
                    name: "Name",
                    fieldType: typeof(string),
                    isFilterable: true,
                    filterOperators: new List<FilterOperator>
                    {
                        FilterOperator.Eq,
                        FilterOperator.Contains
                    },
                    isSearchable: true,
                    searchPriority: namePriority,
                    matchModes: new List<MatchMode>
                    {
                        MatchMode.Contains,
                        MatchMode.Exact
                    },
                    isSortable: true)
            },
            AllowedQueries: new List<RuntimeAllowedQueryMetadata>(),
            Pageable: new RuntimePageableMetadata(
                DefaultSize: 50,
                MaxSize: 500));
    }

    private static RuntimeFieldMetadata CreateField(
        string name,
        Type fieldType,
        bool isFilterable,
        IReadOnlyList<FilterOperator> filterOperators,
        bool isSearchable,
        int? searchPriority,
        IReadOnlyList<MatchMode> matchModes,
        bool isSortable)
    {
        return new RuntimeFieldMetadata(
            Name: name,
            FieldType: fieldType,
            IsFilterable: isFilterable,
            FilterOperators: filterOperators,
            IsSearchable: isSearchable,
            SearchPriority: searchPriority,
            MatchModes: matchModes,
            IsSortable: isSortable);
    }

    private static IReadOnlyDictionary<string, object?> EmptyParameters()
    {
        return new Dictionary<string, object?>();
    }
}
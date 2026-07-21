using Kaleido.Metadata;
using Xunit;

namespace Kaleido.UnitTests;

public sealed class RecordQueryValidatorTests
{
    private readonly RecordQueryValidator _sut = new();

    [Fact]
    public void Validate_Should_Throw_When_Request_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => _sut.Validate(null!, metadata));

        // Assert
        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void Validate_Should_Throw_When_Metadata_Is_Null()
    {
        // Arrange
        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: null,
            Parameters: null);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => _sut.Validate(request, null!));

        // Assert
        Assert.Equal("metadata", exception.ParamName);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Query_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: null,
            Parameters: null);

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Named_Query_Is_Allowed_And_Required_Parameters_Exist()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "ByCode",
            Query: null,
            Parameters: new Dictionary<string, object?>
            {
                ["code"] = "ABC"
            });

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Resolve_Named_Query_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "bycode",
            Query: null,
            Parameters: new Dictionary<string, object?>
            {
                ["code"] = "ABC"
            });

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Named_Query_Is_Not_Allowed()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "MissingQuery",
            Query: null,
            Parameters: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Named query 'MissingQuery' is not allowed for record 'TestRecord'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Named_Query_Required_Parameter_Is_Missing()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "ByCode",
            Query: null,
            Parameters: new Dictionary<string, object?>());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Named query 'ByCode' requires parameter 'code'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Named_Query_Required_Parameter_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: "ByCode",
            Query: null,
            Parameters: new Dictionary<string, object?>
            {
                ["code"] = null
            });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Named query 'ByCode' requires parameter 'code'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Filter_Field_Is_Filterable_And_Operator_Is_Supported()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            filter: FilterCondition(
                field: "Code",
                @operator: FilterOperator.Eq,
                "ABC"));

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Resolve_Filter_Field_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            filter: FilterCondition(
                field: "code",
                @operator: FilterOperator.Eq,
                "ABC"));

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Node_Has_Both_Condition_And_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var node = new QueryFilterNode(
            Condition: new QueryFilterCondition(
                Field: "Code",
                Operator: FilterOperator.Eq,
                Values: new List<object?> { "ABC" }),
            Group: new QueryFilterGroup(
                Operator: LogicalOperator.And,
                Filters: new List<QueryFilterNode>
                {
                    FilterCondition("Name", FilterOperator.Contains, "Test")
                }));

        var request = CreateRequest(filter: node);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Filter node cannot specify both Condition and Group.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Node_Has_Neither_Condition_Nor_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            filter: new QueryFilterNode(
                Condition: null,
                Group: null));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Filter node must specify either Condition or Group.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Field_Is_Missing()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            filter: FilterCondition(
                field: "   ",
                @operator: FilterOperator.Eq,
                "ABC"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Filter field is required.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Field_Does_Not_Exist()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            filter: FilterCondition(
                field: "MissingField",
                @operator: FilterOperator.Eq,
                "ABC"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Field 'MissingField' does not exist on record 'TestRecord'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Field_Is_Not_Filterable()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            filter: FilterCondition(
                field: "Description",
                @operator: FilterOperator.Eq,
                "ABC"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Field 'Description' is not filterable.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Operator_Is_Not_Supported_For_Field()
    {
        // Arrange
        var metadata = CreateMetadata();

        var unsupportedOperator = FilterOperator.Gt;

        var request = CreateRequest(
            filter: FilterCondition(
                field: "Code",
                @operator: unsupportedOperator,
                "ABC"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            $"Operator '{unsupportedOperator}' is not supported for field 'Code'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Filter_Group_Contains_Valid_Expressions()
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

        var request = CreateRequest(filter: filter);

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Filter_Group_Contains_Nested_Group()
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

        var request = CreateRequest(filter: filter);

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Group_Is_Empty()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            filter: new QueryFilterNode(
                Condition: null,
                Group: new QueryFilterGroup(
                    Operator: LogicalOperator.And,
                    Filters: new List<QueryFilterNode>())));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Filter group must contain at least one expression.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Search_Is_Valid_For_Specific_Field()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: SearchCondition(
                searchText: "abc",
                matchMode: MatchMode.Contains,
                field: "Name"));

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Resolve_Search_Field_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: SearchCondition(
                searchText: "abc",
                matchMode: MatchMode.Contains,
                field: "name"));

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Search_Is_Valid_Across_All_Searchable_Fields()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: SearchCondition(
                searchText: "abc",
                matchMode: MatchMode.Contains,
                field: null));

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Search_Node_Has_Both_Condition_And_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var node = new QuerySearchNode(
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

        var request = CreateRequest(search: node);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Search node cannot specify both Condition and Group.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Search_Node_Has_Neither_Condition_Nor_Group()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: new QuerySearchNode(
                Condition: null,
                Group: null));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Search node must specify either Condition or Group.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_SearchText_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: SearchCondition(
                searchText: null!,
                matchMode: MatchMode.Contains,
                field: "Name"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Search text is required.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_SearchText_Is_Whitespace()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: SearchCondition(
                searchText: "   ",
                matchMode: MatchMode.Contains,
                field: "Name"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Search text is required.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Search_Field_Is_Not_Searchable()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: SearchCondition(
                searchText: "abc",
                matchMode: MatchMode.Contains,
                field: "Description"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "No searchable fields exist for search field 'Description'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Search_Field_Does_Not_Exist()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: SearchCondition(
                searchText: "abc",
                matchMode: MatchMode.Contains,
                field: "MissingField"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "No searchable fields exist for search field 'MissingField'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_MatchMode_Is_Not_Supported_For_Search_Field()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: SearchCondition(
                searchText: "abc",
                matchMode: MatchMode.StartsWith,
                field: "Name"));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Match mode 'StartsWith' is not supported for search field 'Name'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_MatchMode_Is_Not_Supported_For_Any_Searchable_Field()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: SearchCondition(
                searchText: "abc",
                matchMode: MatchMode.StartsWith,
                field: null));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Match mode 'StartsWith' is not supported for search field '*'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Search_Group_Contains_Valid_Expressions()
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

        var request = CreateRequest(search: search);

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Search_Group_Contains_Nested_Group()
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

        var request = CreateRequest(search: search);

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Search_Group_Is_Empty()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            search: new QuerySearchNode(
                Condition: null,
                Group: new QuerySearchGroup(
                    Operator: LogicalOperator.And,
                    Searches: new List<QuerySearchNode>())));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Search group must contain at least one expression.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Sort_Field_Is_Sortable()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            sort: new List<QuerySort>
            {
                new QuerySort(
                    Field: "Code",
                    Direction: SortDirection.Ascending)
            });

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Resolve_Sort_Field_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            sort: new List<QuerySort>
            {
                new QuerySort(
                    Field: "code",
                    Direction: SortDirection.Ascending)
            });

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Sort_Field_Does_Not_Exist()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            sort: new List<QuerySort>
            {
                new QuerySort(
                    Field: "MissingField",
                    Direction: SortDirection.Ascending)
            });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Field 'MissingField' does not exist on record 'TestRecord'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Sort_Field_Is_Not_Sortable()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = CreateRequest(
            sort: new List<QuerySort>
            {
                new QuerySort(
                    Field: "Description",
                    Direction: SortDirection.Ascending)
            });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Field 'Description' is not sortable.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Page_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata(pageable: null);

        var request = CreateRequest(page: null);

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Page_Size_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata(
            pageable: new RuntimePageableMetadata(
                DefaultSize: 25,
                MaxSize: 100));

        var request = CreateRequest(
            page: new QueryPage(
                Size: null,
                Offset: 0));

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Record_Does_Not_Support_Paging_And_Page_Is_Provided()
    {
        // Arrange
        var metadata = CreateMetadata(pageable: null);

        var request = CreateRequest(
            page: new QueryPage(
                Size: 10,
                Offset: 0));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Record 'TestRecord' does not support paging.",
            exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Should_Throw_When_Page_Size_Is_Less_Than_Or_Equal_To_Zero(
        int size)
    {
        // Arrange
        var metadata = CreateMetadata(
            pageable: new RuntimePageableMetadata(
                DefaultSize: 25,
                MaxSize: 100));

        var request = CreateRequest(
            page: new QueryPage(
                Size: size,
                Offset: 0));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Page size must be greater than zero.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Page_Size_Exceeds_MaxSize()
    {
        // Arrange
        var metadata = CreateMetadata(
            pageable: new RuntimePageableMetadata(
                DefaultSize: 25,
                MaxSize: 100));

        var request = CreateRequest(
            page: new QueryPage(
                Size: 101,
                Offset: 0));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Page size '101' exceeds max page size '100'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Page_Size_Equals_MaxSize()
    {
        // Arrange
        var metadata = CreateMetadata(
            pageable: new RuntimePageableMetadata(
                DefaultSize: 25,
                MaxSize: 100));

        var request = CreateRequest(
            page: new QueryPage(
                Size: 100,
                Offset: 0));

        // Act
        var exception = Record.Exception(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    private static KaleidoQueryRequest CreateRequest(
        QuerySearchNode? search = null,
        QueryFilterNode? filter = null,
        IReadOnlyList<QuerySort>? sort = null,
        QueryPage? page = null)
    {
        return new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: search,
                Filter: filter,
                Sort: sort,
                Page: page),
            Parameters: null);
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
        RuntimePageableMetadata? pageable = null)
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
            AllowedQueries: new List<RuntimeAllowedQueryMetadata>
            {
                new RuntimeAllowedQueryMetadata(
                    Name: "ByCode",
                    Description: "Find by code.",
                    Parameters: new List<string>
                    {
                        "code"
                    }),

                new RuntimeAllowedQueryMetadata(
                    Name: "All",
                    Description: "All records.",
                    Parameters: new List<string>())
            },
            Pageable: pageable);
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
}
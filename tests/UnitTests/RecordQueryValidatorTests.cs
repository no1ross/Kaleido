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
    public void Validate_Should_Not_Throw_When_Query_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: null,
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

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
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

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
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: new QueryFilter(
                    Field: "Code",
                    Operator: FilterOperator.Eq,
                    Values: new List<object?> { "ABC" }),
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Resolve_Filter_Field_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: new QueryFilter(
                    Field: "code",
                    Operator: FilterOperator.Eq,
                    Values: new List<object?> { "ABC" }),
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Field_Does_Not_Exist()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: new QueryFilter(
                    Field: "MissingField",
                    Operator: FilterOperator.Eq,
                    Values: new List<object?> { "ABC" }),
                Sort: null,
                Page: null),
            Parameters: null);

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: new QueryFilter(
                    Field: "Description",
                    Operator: FilterOperator.Eq,
                    Values: new List<object?> { "ABC" }),
                Sort: null,
                Page: null),
            Parameters: null);

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: new QueryFilter(
                    Field: "Code",
                    Operator: FilterOperator.Gt,
                    Values: new List<object?> { "ABC" }),
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Operator 'Gt' is not supported for field 'Code'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Filter_Group_Contains_Valid_Expressions()
    {
        // Arrange
        var metadata = CreateMetadata();

        var filter = new QueryFilterGroup(
            Operator: LogicalOperator.And,
            Expressions: new List<IFilterExpression>
            {
                new QueryFilter(
                    Field: "Code",
                    Operator: FilterOperator.Eq,
                    Values: new List<object?> { "ABC" }),

                new QueryFilter(
                    Field: "Name",
                    Operator: FilterOperator.Contains,
                    Values: new List<object?> { "Test" })
            });

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: filter,
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Group_Is_Empty()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: new QueryFilterGroup(
                    Operator: LogicalOperator.And,
                    Expressions: new List<IFilterExpression>()),
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Filter group must contain at least one expression.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Filter_Expression_Type_Is_Unsupported()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: new UnsupportedFilterExpression(),
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Unsupported filter expression type 'UnsupportedFilterExpression'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Search_Is_Valid_For_Specific_Field()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new QuerySearch(
                    SearchText: "abc",
                    MatchMode: MatchMode.Contains,
                    Field: "Name"),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Resolve_Search_Field_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new QuerySearch(
                    SearchText: "abc",
                    MatchMode: MatchMode.Contains,
                    Field: "name"),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Search_Is_Valid_Across_All_Searchable_Fields()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new QuerySearch(
                    SearchText: "abc",
                    MatchMode: MatchMode.Contains,
                    Field: null),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_SearchText_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new QuerySearch(
                    SearchText: null!,
                    MatchMode: MatchMode.Contains,
                    Field: "Name"),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal("Search text is required.", exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_SearchText_Is_Whitespace()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new QuerySearch(
                    SearchText: "   ",
                    MatchMode: MatchMode.Contains,
                    Field: "Name"),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal("Search text is required.", exception.Message);
    }

    [Fact]
    public void Validate_Should_Throw_When_Search_Field_Is_Not_Searchable()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new QuerySearch(
                    SearchText: "abc",
                    MatchMode: MatchMode.Contains,
                    Field: "Description"),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new QuerySearch(
                    SearchText: "abc",
                    MatchMode: MatchMode.Contains,
                    Field: "MissingField"),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new QuerySearch(
                    SearchText: "abc",
                    MatchMode: MatchMode.StartsWith,
                    Field: "Name"),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new QuerySearch(
                    SearchText: "abc",
                    MatchMode: MatchMode.StartsWith,
                    Field: null),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

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

        var search = new QuerySearchGroup(
            Operator: LogicalOperator.And,
            Expressions: new List<ISearchExpression>
            {
                new QuerySearch(
                    SearchText: "abc",
                    MatchMode: MatchMode.Contains,
                    Field: "Code"),

                new QuerySearch(
                    SearchText: "test",
                    MatchMode: MatchMode.Contains,
                    Field: "Name")
            });

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: search,
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Search_Expression_Type_Is_Unsupported()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: new UnsupportedSearchExpression(),
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => _sut.Validate(request, metadata));

        // Assert
        Assert.Equal(
            "Unsupported search expression type 'UnsupportedSearchExpression'.",
            exception.Message);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Sort_Field_Is_Sortable()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: new List<QuerySort>
                {
                    new QuerySort(
                        Field: "Code",
                        Direction: SortDirection.Ascending)
                },
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Resolve_Sort_Field_Case_Insensitive()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: new List<QuerySort>
                {
                    new QuerySort(
                        Field: "code",
                        Direction: SortDirection.Ascending)
                },
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Sort_Field_Does_Not_Exist()
    {
        // Arrange
        var metadata = CreateMetadata();

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: new List<QuerySort>
                {
                    new QuerySort(
                        Field: "MissingField",
                        Direction: SortDirection.Ascending)
                },
                Page: null),
            Parameters: null);

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: new List<QuerySort>
                {
                    new QuerySort(
                        Field: "Description",
                        Direction: SortDirection.Ascending)
                },
                Page: null),
            Parameters: null);

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: null),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Not_Throw_When_Page_Size_Is_Null()
    {
        // Arrange
        var metadata = CreateMetadata(new RuntimePageableMetadata(25, 100));

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: new QueryPage(
                    Size: null,
                    Offset: 0)),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_Should_Throw_When_Record_Does_Not_Support_Paging_And_Page_Is_Provided()
    {
        // Arrange
        var metadata = CreateMetadata(pageable: null);

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: new QueryPage(
                    Size: 10,
                    Offset: 0)),
            Parameters: null);

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
    public void Validate_Should_Throw_When_Page_Size_Is_Less_Than_Or_Equal_To_Zero(int size)
    {
        // Arrange
        var metadata = CreateMetadata(new RuntimePageableMetadata(25, 100));

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: new QueryPage(
                    Size: size,
                    Offset: 0)),
            Parameters: null);

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: new QueryPage(
                    Size: 101,
                    Offset: 0)),
            Parameters: null);

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

        var request = new KaleidoQueryRequest(
            QueryName: null,
            Query: new KaleidoQueryBody(
                Search: null,
                Filter: null,
                Sort: null,
                Page: new QueryPage(
                    Size: 100,
                    Offset: 0)),
            Parameters: null);

        // Act
        var exception = Record.Exception(() => _sut.Validate(request, metadata));

        // Assert
        Assert.Null(exception);
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
            Pageable: pageable
        );
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

    private sealed class UnsupportedFilterExpression : IFilterExpression
    {
    }

    private sealed class UnsupportedSearchExpression : ISearchExpression
    {
    }
}
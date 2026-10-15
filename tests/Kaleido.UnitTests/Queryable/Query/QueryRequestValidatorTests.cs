using Kaleido.Exceptions;
using Kaleido.Registry;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryRequestValidatorTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly DataTypeDescriptor TestDataType =
        new("string");

    private readonly QueryRequestValidator _validator = CreateSut();

    private static QueryRequestValidator CreateSut()
    {
        var typeDescriber = new Mock<ITypeDescriber>();
        typeDescriber
            .Setup(m => m.IsSupportedType(It.IsAny<Type>()))
            .Returns(true);

        return new QueryRequestValidator(typeDescriber.Object);
    }

    [Fact]
    public void Validate_WhenRequestIsValid_DoesNotThrow()
    {
        var request = new QueryRequest(
            new QueryBody(
                Filter: QueryFilterNode.CreateCondition("Code", FilterOperator.Equals, "A"),
                Sort: [new QuerySort("Code", SortDirection.Ascending)],
                Page: new QueryPage(10, 0)));

        Validate(request, CreateMetadata());
    }

    [Fact]
    public void Validate_WhenFilterFieldIsMissing_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("", FilterOperator.Equals, "A")));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.MissingFilterField, ex.Code);
    }

    [Fact]
    public void Validate_WhenFieldDoesNotExist_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Missing", FilterOperator.Equals, "A")));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.InvalidField, ex.Code);
    }

    [Fact]
    public void Validate_WhenFieldIsNotFilterable_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Description", FilterOperator.Equals, "A")));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.FieldNotFilterable, ex.Code);
    }

    [Fact]
    public void Validate_WhenSortContainsDuplicates_Throws()
    {
        var request = new QueryRequest(new QueryBody(Sort: [new QuerySort("Code", SortDirection.Ascending), new QuerySort("Code", SortDirection.Descending)]));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.DuplicateSortField, ex.Code);
    }

    [Fact]
    public void Validate_WhenSortFieldIsNotSortable_Throws()
    {
        var request = new QueryRequest(new QueryBody(Sort: [new QuerySort("Description", SortDirection.Ascending)]));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.FieldNotSortable, ex.Code);
    }

    [Fact]
    public void Validate_WhenFilterGroupIsEmpty_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateGroup(LogicalOperator.And)));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.EmptyFilterGroup, ex.Code);
    }

    [Fact]
    public void Validate_WhenPageSizeExceedsMaximum_Throws()
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(999, 0)));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.InvalidPageSize, ex.Code);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, 20)]
    public void Validate_WhenPageOmitsSize_DoesNotThrow(int? size, int? offset)
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(size, offset)));

        Validate(request, CreateMetadata());
    }

    [Fact]
    public void Validate_WhenSearchHasNoSearchableFields_Throws()
    {
        var request = new QueryRequest(new QueryBody(SearchText: "abc"));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadataWithoutSearchableFields()));
        Assert.Equal(QueryableErrorCodes.FieldNotSearchable, ex.Code);
    }

    [Fact]
    public void Validate_WhenPageRequestedButNotPageable_Throws()
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(10, 0)));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateMetadata(), pageable: null));
        Assert.Equal(QueryableErrorCodes.PagingNotSupported, ex.Code);
    }

    [Fact]
    public void Validate_WhenParametersWithARequiredMemberAreMissing_Throws()
    {
        var request = new QueryRequest<ScopedParameters>(ViewParameters: null);

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.MissingParameter, ex.Code);
        Assert.Equal("Parameter 'ProcessId' is required.", ex.Message);
    }

    [Fact]
    public void Validate_WhenARequiredParameterIsNull_Throws()
    {
        var request = new QueryRequest<ScopedParameters>(new ScopedParameters { ProcessId = null });

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.MissingParameter, ex.Code);
        Assert.Equal("Parameter 'ProcessId' is required.", ex.Message);
    }

    [Fact]
    public void Validate_WhenAParameterFailsAnotherAttribute_Throws()
    {
        var request = new QueryRequest<ScopedParameters>(new ScopedParameters { ProcessId = Guid.NewGuid(), Limit = 500 });

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));
        Assert.Equal(QueryableErrorCodes.InvalidParameterValue, ex.Code);
    }

    [Fact]
    public void Validate_WhenParametersAreValid_DoesNotThrow()
    {
        var request = new QueryRequest<ScopedParameters>(new ScopedParameters { ProcessId = Guid.NewGuid(), Limit = 5 });

        Validate(request, CreateMetadata());
    }

    [Fact]
    public void Validate_WhenOptionalParametersAreMissing_DoesNotThrow()
    {
        var request = new QueryRequest<OptionalParameters>(ViewParameters: null);

        Validate(request, CreateMetadata());
    }

    private sealed class ScopedParameters : IQueryParameters
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid? ProcessId { get; init; }

        [System.ComponentModel.DataAnnotations.Range(1, 100)]
        public int Limit { get; init; } = 10;
    }

    private sealed class OptionalParameters : IQueryParameters
    {
        public string? State { get; init; }
    }

    private void Validate(IQueryRequest request, QuerySourceMetadata metadata) =>
        _validator.Validate(request, metadata, metadata.Pageable);

    private static QuerySourceMetadata CreateMetadata() =>
        CreateMetadata(
            [
                new FieldMetadata { Name = "Code", Description = null, FieldType = typeof(string), DataType = TestDataType, IsFilterable = true, FilterOperators = [FilterOperator.Equals], IsSearchable = false, SearchPriority = null, MatchMode = null, IsSortable = true },
                new FieldMetadata { Name = "Description", Description = null, FieldType = typeof(string), DataType = TestDataType, IsFilterable = false, FilterOperators = [], IsSearchable = true, SearchPriority = 1, MatchMode = MatchMode.Contains, IsSortable = false }
            ]);

    private static QuerySourceMetadata CreateMetadataWithoutSearchableFields() =>
        CreateMetadata(
            [new FieldMetadata { Name = "Code", Description = null, FieldType = typeof(string), DataType = TestDataType, IsFilterable = true, FilterOperators = [FilterOperator.Equals], IsSearchable = false, SearchPriority = null, MatchMode = null, IsSortable = true }]);

    private static QuerySourceMetadata CreateMetadata(IReadOnlyList<FieldMetadata> fields) =>
        new(
            "TestSource",
            "Test source",
            "Test Source",
            "1.0.0",
            "Unit Test",
            QuerySourceKind.Local,
            new PageableMetadata(25, 100),
            fields,
            [],
            [],
            AuthorizationMetadata.Unspecified);
}

using Kaleido.Exceptions;
using Kaleido.Registry;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryRequestValidatorTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly DataTypeDescriptor TestDataType =
        new("string");

    private readonly QueryRequestValidator _validator = CreateSut();

    private static QueryRequestValidator CreateSut(bool supportedTypes = true)
    {
        var typeDescriber = new Mock<ITypeDescriber>();
        typeDescriber
            .Setup(m => m.IsSupportedType(It.IsAny<Type>()))
            .Returns(supportedTypes);

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

    [Fact]
    public void Validate_WhenRequestOrMetadataIsNull_Throws()
    {
        Assert.Equal("request", Assert.Throws<ArgumentNullException>(() => _validator.Validate(null!, CreateMetadata(), null)).ParamName);
        Assert.Equal("metadata", Assert.Throws<ArgumentNullException>(() => _validator.Validate(new QueryRequest(), null!, null)).ParamName);
    }

    [Fact]
    public void Validate_WhenAFilterValueHasAnUnsupportedRuntimeType_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Code", FilterOperator.Equals, new object())));

        var ex = Assert.Throws<KaleidoValidationException>(() => CreateSut(supportedTypes: false).Validate(request, CreateMetadata(), null));

        Assert.Equal(QueryableErrorCodes.UnsupportedRuntimeType, ex.Code);
        Assert.Contains("System.Object", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NullFilterValues_AreNotTypeChecked()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Code", FilterOperator.Equals, [null])));

        CreateSut(supportedTypes: false).Validate(request, CreateMetadata(), null);
    }

    [Fact]
    public void Validate_WhenTheFieldDoesNotSupportTheOperator_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Code", FilterOperator.Contains, "A")));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));

        Assert.Equal(QueryableErrorCodes.UnsupportedOperator, ex.Code);
    }

    [Fact]
    public void Validate_WhenAGroupHasValidConditions_DoesNotThrow()
    {
        var request = new QueryRequest(
            new QueryBody(
                Filter: QueryFilterNode.CreateGroup(
                    LogicalOperator.Or,
                    QueryFilterNode.CreateCondition("Code", FilterOperator.Equals, "A"),
                    QueryFilterNode.CreateGroup(LogicalOperator.And, QueryFilterNode.CreateCondition("Code", FilterOperator.Equals, "B")))));

        Validate(request, CreateMetadata());
    }

    [Fact]
    public void Validate_WhenAChildConditionIsInvalid_Throws()
    {
        var request = new QueryRequest(
            new QueryBody(Filter: QueryFilterNode.CreateGroup(LogicalOperator.And, QueryFilterNode.CreateCondition("Description", FilterOperator.Equals, "A"))));

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));

        Assert.Equal(QueryableErrorCodes.FieldNotFilterable, ex.Code);
    }

    [Fact]
    public void Validate_WhenTheFilterIsNestedTooDeeply_Throws()
    {
        var node = QueryFilterNode.CreateCondition("Code", FilterOperator.Equals, "A");
        for (var i = 0; i < 11; i++)
        {
            node = QueryFilterNode.CreateGroup(LogicalOperator.And, node);
        }

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(new QueryRequest(new QueryBody(Filter: node)), CreateMetadata()));

        Assert.Equal(QueryableErrorCodes.FilterDepthExceeded, ex.Code);
    }

    [Fact]
    public void Validate_AtTheMaximumFilterDepth_DoesNotThrow()
    {
        var node = QueryFilterNode.CreateCondition("Code", FilterOperator.Equals, "A");
        for (var i = 0; i < 10; i++)
        {
            node = QueryFilterNode.CreateGroup(LogicalOperator.And, node);
        }

        Validate(new QueryRequest(new QueryBody(Filter: node)), CreateMetadata());
    }

    [Fact]
    public void Validate_WhenANodeHasConditionAndGroup_Throws()
    {
        var condition = new QueryFilterCondition("Code", FilterOperator.Equals, ["A"]);
        var group = new QueryFilterGroup(LogicalOperator.And, [QueryFilterNode.CreateCondition("Code", FilterOperator.Equals, "A")]);

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(new QueryRequest(new QueryBody(Filter: new QueryFilterNode(condition, group))), CreateMetadata()));

        Assert.Equal(QueryableErrorCodes.InvalidFilterNode, ex.Code);
        Assert.Contains("both", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenANodeHasNeitherConditionNorGroup_Throws()
    {
        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(new QueryRequest(new QueryBody(Filter: new QueryFilterNode(null, null))), CreateMetadata()));

        Assert.Equal(QueryableErrorCodes.InvalidFilterNode, ex.Code);
        Assert.Contains("either", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData(" ")]
    public void Validate_SearchWithSearchableFieldsOrBlankText_DoesNotThrow(string searchText)
    {
        Validate(new QueryRequest(new QueryBody(SearchText: searchText)), CreateMetadata());
        _validator.Validate(new QueryRequest(new QueryBody(SearchText: " ")), CreateMetadataWithoutSearchableFields(), null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenThePageSizeIsNotPositive_Throws(int size)
    {
        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(new QueryRequest(new QueryBody(Page: new QueryPage(size, 0))), CreateMetadata()));

        Assert.Equal(QueryableErrorCodes.InvalidPageSize, ex.Code);
        Assert.Contains("greater than zero", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenThePageOffsetIsNegative_Throws()
    {
        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(new QueryRequest(new QueryBody(Page: new QueryPage(10, -1))), CreateMetadata()));

        Assert.Equal(QueryableErrorCodes.InvalidPageOffset, ex.Code);
    }

    [Fact]
    public void Validate_WhenARequiredParameterHasACustomMessage_UsesIt()
    {
        var request = new QueryRequest<CustomMessageParameters>(new CustomMessageParameters());

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));

        Assert.Equal(QueryableErrorCodes.MissingParameter, ex.Code);
        Assert.Equal("Choose a member first.", ex.Message);
    }

    [Fact]
    public void Validate_WhenAParameterFailsWithoutAMessage_UsesTheAttributesDefaultMessage()
    {
        var request = new QueryRequest<SilentFailureParameters>(new SilentFailureParameters { Code = "x" });

        var ex = Assert.Throws<KaleidoValidationException>(() => Validate(request, CreateMetadata()));

        Assert.Equal(QueryableErrorCodes.InvalidParameterValue, ex.Code);
        Assert.Contains("Code", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenParametersHaveMembersWithoutAttributes_SkipsThem()
    {
        Validate(new QueryRequest<OptionalParameters>(new OptionalParameters { State = "open" }), CreateMetadata());
    }

    private sealed class CustomMessageParameters : IQueryParameters
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Choose a member first.")]
        public string? MemberId { get; init; }
    }

    private sealed class SilentFailureParameters : IQueryParameters
    {
        [SilentFailure]
        public string? Code { get; init; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class SilentFailureAttribute : System.ComponentModel.DataAnnotations.ValidationAttribute
    {
        protected override System.ComponentModel.DataAnnotations.ValidationResult? IsValid(object? value, System.ComponentModel.DataAnnotations.ValidationContext validationContext) =>
            new(null);
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

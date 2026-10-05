using Kaleido.Exceptions;

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

        _validator.Validate(request, CreateRegistration());
    }

    [Fact]
    public void Validate_WhenFilterFieldIsMissing_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("", FilterOperator.Equals, "A")));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(QueryableErrorCodes.MissingFilterField, ex.Code);
    }

    [Fact]
    public void Validate_WhenFieldDoesNotExist_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Missing", FilterOperator.Equals, "A")));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(QueryableErrorCodes.InvalidField, ex.Code);
    }

    [Fact]
    public void Validate_WhenFieldIsNotFilterable_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Description", FilterOperator.Equals, "A")));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(QueryableErrorCodes.FieldNotFilterable, ex.Code);
    }

    [Fact]
    public void Validate_WhenSortContainsDuplicates_Throws()
    {
        var request = new QueryRequest(new QueryBody(Sort: [new QuerySort("Code", SortDirection.Ascending), new QuerySort("Code", SortDirection.Descending)]));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(QueryableErrorCodes.DuplicateSortField, ex.Code);
    }

    [Fact]
    public void Validate_WhenSortFieldIsNotSortable_Throws()
    {
        var request = new QueryRequest(new QueryBody(Sort: [new QuerySort("Description", SortDirection.Ascending)]));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(QueryableErrorCodes.FieldNotSortable, ex.Code);
    }

    [Fact]
    public void Validate_WhenFilterGroupIsEmpty_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateGroup(LogicalOperator.And)));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(QueryableErrorCodes.EmptyFilterGroup, ex.Code);
    }

    [Fact]
    public void Validate_WhenPageSizeExceedsMaximum_Throws()
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(999, 0)));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(QueryableErrorCodes.InvalidPageSize, ex.Code);
    }

    [Fact]
    public void Validate_WhenSearchHasNoSearchableFields_Throws()
    {
        var request = new QueryRequest(new QueryBody(SearchText: "abc"));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistrationWithoutSearchableFields()));
        Assert.Equal(QueryableErrorCodes.FieldNotSearchable, ex.Code);
    }

    private static QueryContextRegistration CreateRegistration() =>
        new(
            typeof(TestRecord),
            typeof(object),
            new QueryContextMetadata(
                "test-record",
                "Test Record",
                "Test Record",
                "1.0.0",
                "Unit Test",
                QueryContextKind.Direct,
                new PageableMetadata(25, 100),
                [
                    new FieldMetadata("Code", null, typeof(string), TestDataType, true, [FilterOperator.Equals], false, null, null, true),
                    new FieldMetadata("Description", null, typeof(string), TestDataType, false, [], true, 1, MatchMode.Contains, false)
                ]));

    private static QueryContextRegistration CreateRegistrationWithoutSearchableFields() =>
        new(
            typeof(TestRecord),
            typeof(object),
            new QueryContextMetadata(
                "test-record",
                "Test Record",
                "Test Record",
                "1.0.0",
                "Unit Test",
                QueryContextKind.Direct,
                new PageableMetadata(25, 100),
                [new FieldMetadata("Code", null, typeof(string), TestDataType, true, [FilterOperator.Equals], false, null, null, true)]));

    private sealed class TestRecord
    {
    }
}

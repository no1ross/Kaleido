using Kaleido.Exceptions;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class QueryRequestValidatorTests
{
    private readonly QueryRequestValidator _validator = new();

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
        Assert.Equal(ValidationErrorCodes.QryMissingFilterField, ex.Code);
    }

    [Fact]
    public void Validate_WhenFieldDoesNotExist_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Missing", FilterOperator.Equals, "A")));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(ValidationErrorCodes.QryInvalidField, ex.Code);
    }

    [Fact]
    public void Validate_WhenFieldIsNotFilterable_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Description", FilterOperator.Equals, "A")));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(ValidationErrorCodes.QryFieldNotFilterable, ex.Code);
    }

    [Fact]
    public void Validate_WhenSortContainsDuplicates_Throws()
    {
        var request = new QueryRequest(new QueryBody(Sort: [new QuerySort("Code", SortDirection.Ascending), new QuerySort("Code", SortDirection.Descending)]));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(ValidationErrorCodes.QryDuplicateSortField, ex.Code);
    }

    [Fact]
    public void Validate_WhenSortFieldIsNotSortable_Throws()
    {
        var request = new QueryRequest(new QueryBody(Sort: [new QuerySort("Description", SortDirection.Ascending)]));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(ValidationErrorCodes.QryFieldNotSortable, ex.Code);
    }

    [Fact]
    public void Validate_WhenFilterGroupIsEmpty_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateGroup(LogicalOperator.And)));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(ValidationErrorCodes.QryEmptyFilterGroup, ex.Code);
    }

    [Fact]
    public void Validate_WhenPageSizeExceedsMaximum_Throws()
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(999, 0)));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistration()));
        Assert.Equal(ValidationErrorCodes.QryInvalidPageSize, ex.Code);
    }

    [Fact]
    public void Validate_WhenSearchHasNoSearchableFields_Throws()
    {
        var request = new QueryRequest(new QueryBody(SearchText: "abc"));

        var ex = Assert.Throws<KaleidoValidationException>(() => _validator.Validate(request, CreateRegistrationWithoutSearchableFields()));
        Assert.Equal(ValidationErrorCodes.QryFieldNotSearchable, ex.Code);
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
                    new FieldMetadata("Code", null, typeof(string), DataTypeMapper.GetDescriptor(typeof(TestRecordMetadata).GetProperty(nameof(TestRecordMetadata.Code))!), true, [FilterOperator.Equals], false, null, null, true),
                    new FieldMetadata("Description", null, typeof(string), DataTypeMapper.GetDescriptor(typeof(TestRecordMetadata).GetProperty(nameof(TestRecordMetadata.Description))!), false, [], true, 1, MatchMode.Contains, false)
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
                [new FieldMetadata("Code", null, typeof(string), DataTypeMapper.GetDescriptor(typeof(TestRecordMetadata).GetProperty(nameof(TestRecordMetadata.Code))!), true, [FilterOperator.Equals], false, null, null, true)]));

    private sealed class TestRecord
    {
    }

    private sealed class TestRecordMetadata
    {
        public string Code { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;
    }
}

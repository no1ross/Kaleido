using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class QueryRequestValidatorTests
{
    private readonly QueryRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenRequestIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.Validate(null!, CreateRegistration()));
    }

    [Fact]
    public void Validate_WhenRegistrationIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _validator.Validate(new QueryRequest(), null!));
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

        Assert.Throws<MissingFilterFieldException>(() => _validator.Validate(request, CreateRegistration()));
    }

    [Fact]
    public void Validate_WhenFieldDoesNotExist_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Missing", FilterOperator.Equals, "A")));

        Assert.Throws<InvalidFieldException>(() => _validator.Validate(request, CreateRegistration()));
    }

    [Fact]
    public void Validate_WhenFieldIsNotFilterable_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateCondition("Description", FilterOperator.Equals, "A")));

        Assert.Throws<FieldNotFilterableException>(() => _validator.Validate(request, CreateRegistration()));
    }

    [Fact]
    public void Validate_WhenSortContainsDuplicates_Throws()
    {
        var request = new QueryRequest(new QueryBody(Sort: [new QuerySort("Code", SortDirection.Ascending), new QuerySort("Code", SortDirection.Descending)]));

        Assert.Throws<DuplicateSortFieldException>(() => _validator.Validate(request, CreateRegistration()));
    }

    [Fact]
    public void Validate_WhenSortFieldIsNotSortable_Throws()
    {
        var request = new QueryRequest(new QueryBody(Sort: [new QuerySort("Description", SortDirection.Ascending)]));

        Assert.Throws<FieldNotSortableException>(() => _validator.Validate(request, CreateRegistration()));
    }

    [Fact]
    public void Validate_WhenFilterGroupIsEmpty_Throws()
    {
        var request = new QueryRequest(new QueryBody(Filter: QueryFilterNode.CreateGroup(LogicalOperator.And)));

        Assert.Throws<EmptyFilterGroupException>(() => _validator.Validate(request, CreateRegistration()));
    }

    [Fact]
    public void Validate_WhenPageSizeExceedsMaximum_Throws()
    {
        var request = new QueryRequest(new QueryBody(Page: new QueryPage(999, 0)));

        Assert.Throws<InvalidPageSizeException>(() => _validator.Validate(request, CreateRegistration()));
    }

    [Fact]
    public void Validate_WhenSearchHasNoSearchableFields_Throws()
    {
        var request = new QueryRequest(new QueryBody(SearchText: "abc"));

        Assert.Throws<FieldNotSearchableException>(() => _validator.Validate(request, CreateRegistrationWithoutSearchableFields()));
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

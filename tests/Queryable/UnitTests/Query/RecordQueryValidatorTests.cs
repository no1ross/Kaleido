using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Xunit;

namespace Kaleido.Queryable.UnitTests.Query;

public sealed class RecordQueryValidatorTests
{
    private readonly QueryRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldThrow_WhenRequestIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => _validator.Validate(
                null!,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenRegistrationIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => _validator.Validate(
                new QueryRequest(),
                null!));
    }

    [Fact]
    public void Validate_ShouldNotThrow_WhenRequestIsValid()
    {
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "active"));

        _validator.Validate(
            request,
            CreateRegistration());
    }

    [Fact]
    public void Validate_ShouldThrow_WhenNamedQueryIsNotAllowed()
    {
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "does-not-exist"));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenRequiredNamedQueryParameterIsMissing()
    {
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "by-name",
                    new Dictionary<string, object?>()));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenNamedQueryParameterHasIncorrectType()
    {
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "by-name",
                    new Dictionary<string, object?>
                    {
                        ["Name"] = 123
                    }));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenFilterFieldDoesNotExist()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter:
                        QueryFilterNode.CreateCondition(
                            "BadField",
                            FilterOperator.Equals,
                            "value")));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenFieldIsNotFilterable()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter:
                        QueryFilterNode.CreateCondition(
                            "Description",
                            FilterOperator.Equals,
                            "value")));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenSortFieldIsNotSortable()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            "Description",
                            SortDirection.Ascending)
                    ]));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenDuplicateSortFieldsExist()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Sort:
                    [
                        new QuerySort(
                            "Name",
                            SortDirection.Ascending),

                        new QuerySort(
                            "Name",
                            SortDirection.Descending)
                    ]));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    public void Validate_ShouldThrow_WhenPageSizeExceedsMaximum()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Page:
                        new QueryPage(
                            999,
                            0)));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenFilterGroupIsEmpty()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter:
                        QueryFilterNode.CreateGroup(
                            LogicalOperator.And)));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldValidateNestedFilterGroups()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter:
                        QueryFilterNode.CreateGroup(
                            LogicalOperator.And,

                            QueryFilterNode.CreateCondition(
                                "Name",
                                FilterOperator.Equals,
                                "Test"),

                            QueryFilterNode.CreateGroup(
                                LogicalOperator.Or,

                                QueryFilterNode.CreateCondition(
                                    "Name",
                                    FilterOperator.Equals,
                                    "A"),

                                QueryFilterNode.CreateCondition(
                                    "Name",
                                    FilterOperator.Equals,
                                    "B")))));

        _validator.Validate(
            request,
            CreateRegistration());
    }

    [Fact]
    public void Validate_ShouldThrow_WhenNestedFilterContainsInvalidField()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Filter:
                        QueryFilterNode.CreateGroup(
                            LogicalOperator.And,

                            QueryFilterNode.CreateCondition(
                                "BadField",
                                FilterOperator.Equals,
                                "Test"))));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldAllowMissingOptionalNamedQueryParameter()
    {
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "by-amount",
                    new Dictionary<string, object?>()));

        _validator.Validate(
            request,
            CreateRegistration());
    }

    [Fact]
    public void Validate_ShouldAllowOptionalNamedQueryParameter_WhenTypeIsValid()
    {
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "by-amount",
                    new Dictionary<string, object?>
                    {
                        ["Amount"] = 250m
                    }));

        _validator.Validate(
            request,
            CreateRegistration());
    }

    [Fact]
    public void Validate_ShouldThrow_WhenOptionalNamedQueryParameterHasIncorrectType()
    {
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "by-amount",
                    new Dictionary<string, object?>
                    {
                        ["Amount"] = "not-a-decimal"
                    }));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldAllowMissingOptionalNamedQueryParameter_WhenDefaultValueExists()
    {
        var request =
            new QueryRequest(
                NamedQuery: new NamedQuery(
                    "by-amount",
                    new Dictionary<string, object?>()));

        _validator.Validate(
            request,
            CreateRegistration());
    }

    private static QueryContextRegistration CreateRegistration()
    {
        return new QueryRegistration(
            typeof(TestRecord),
            typeof(TestRecordSource),
            new QueryMetadata(
                "test-record",
                "Test Record",
                "Test Record",
                "1.0.0",
                "Unit Test",
                [
                    new FieldMetadata(
                        "Name",
                        null,
                        typeof(string),
                        true,
                        [FilterOperator.Equals],
                        true,
                        1,
                        MatchMode.Contains,
                        true),

                    new FieldMetadata(
                        "Description",
                        null,
                        typeof(string),
                        false,
                        [],
                        false,
                        null,
                        null,
                        false)
                ],
                new PageableMetadata(
                    25,
                    100)),
            [
                new NamedQueryRegistration(
                    typeof(TestNamedQuery),
                    new NamedQueryMetadata(
                        "active",
                        "Active Records",
                        "Active Records",
                        null)),

                new NamedQueryRegistration(
                    typeof(TestNamedQuery),
                    new NamedQueryMetadata(
                        "by-name",
                        "By Name",
                        "By Name",
                        [
                            new QueryParameterMetadata(
                                "Name",
                                typeof(string),
                                true,
                                "Name to search for",
                                null)
                        ])),
                 new NamedQueryRegistration(
                    typeof(TestNamedQuery),
                    new NamedQueryMetadata(
                        "by-amount",
                        "By Amount",
                        "By Amount",
                        [
                            new QueryParameterMetadata(
                                "Amount",
                                typeof(decimal),
                                false,
                                "Minimum amount",
                                100m)
                        ]))
            ]);
    }

    private sealed record TestRecord(
        string Name);

    private sealed class TestRecordSource
    {
    }

    private sealed class TestNamedQuery
    {
    }


}
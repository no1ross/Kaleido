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
                NamedQuery: new KaleidoNamedQuery(
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
                NamedQuery: new KaleidoNamedQuery(
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
                NamedQuery: new KaleidoNamedQuery(
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
                NamedQuery: new KaleidoNamedQuery(
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
                            FilterOperator.Eq,
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
                            FilterOperator.Eq,
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

    [Fact]
    public void Validate_ShouldThrow_WhenSearchFieldIsNotSearchable()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search:
                        QuerySearchNode.CreateCondition(
                            "test",
                            MatchMode.Contains,
                            "Description")));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
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
                                FilterOperator.Eq,
                                "Test"),

                            QueryFilterNode.CreateGroup(
                                LogicalOperator.Or,

                                QueryFilterNode.CreateCondition(
                                    "Name",
                                    FilterOperator.Eq,
                                    "A"),

                                QueryFilterNode.CreateCondition(
                                    "Name",
                                    FilterOperator.Eq,
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
                                FilterOperator.Eq,
                                "Test"))));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenSearchGroupIsEmpty()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search:
                        QuerySearchNode.CreateGroup(
                            LogicalOperator.And)));

        Assert.Throws<InvalidOperationException>(
            () => _validator.Validate(
                request,
                CreateRegistration()));
    }

    [Fact]
    public void Validate_ShouldValidateNestedSearchGroups()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search:
                        QuerySearchNode.CreateGroup(
                            LogicalOperator.And,

                            QuerySearchNode.CreateCondition(
                                "abc",
                                MatchMode.Contains,
                                "Name"),

                            QuerySearchNode.CreateGroup(
                                LogicalOperator.Or,

                                QuerySearchNode.CreateCondition(
                                    "one",
                                    MatchMode.Contains,
                                    "Name"),

                                QuerySearchNode.CreateCondition(
                                    "two",
                                    MatchMode.Contains,
                                    "Name")))));

        _validator.Validate(
            request,
            CreateRegistration());
    }

    [Fact]
    public void Validate_ShouldThrow_WhenNestedSearchContainsInvalidField()
    {
        var request =
            new QueryRequest(
                Query: new QueryBody(
                    Search:
                        QuerySearchNode.CreateGroup(
                            LogicalOperator.And,

                            QuerySearchNode.CreateCondition(
                                "abc",
                                MatchMode.Contains,
                                "BadField"))));

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
                NamedQuery: new KaleidoNamedQuery(
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
                NamedQuery: new KaleidoNamedQuery(
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
                NamedQuery: new KaleidoNamedQuery(
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
                NamedQuery: new KaleidoNamedQuery(
                    "by-amount",
                    new Dictionary<string, object?>()));

        _validator.Validate(
            request,
            CreateRegistration());
    }

    private static RecordRegistration CreateRegistration()
    {
        return new RecordRegistration(
            typeof(TestRecord),
            typeof(TestRecordSource),
            new RecordMetadata(
                "test-record",
                "Test Record",
                "1.0.0",
                "Unit Test",
                [
                    new FieldMetadata(
                        "Name",
                        typeof(string),
                        true,
                        [FilterOperator.Eq],
                        true,
                        1,
                        [MatchMode.Exact, MatchMode.Contains],
                        true),

                    new FieldMetadata(
                        "Description",
                        typeof(string),
                        false,
                        [],
                        false,
                        null,
                        [],
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
                        null)),

                new NamedQueryRegistration(
                    typeof(TestNamedQuery),
                    new NamedQueryMetadata(
                        "by-name",
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
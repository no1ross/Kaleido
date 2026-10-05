using Kaleido.Exceptions;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryRequestTests
    : Kaleido.UnitTests.SutFixture<QueryRequest>
{
    protected override QueryRequest CreateSut() =>
        new();

    private static QueryRequest<TestParameters> CreateSut(
        TestParameters parameters,
        QueryBody? query) =>
        new(parameters, query);

    [Fact]
    public void QueryRequest_UsesEmptyQueryViewParametersByDefault()
    {
        IQueryRequest request = CreateSut();

        Assert.IsType<EmptyQueryViewParameters>(request.ViewParameters);
        Assert.Equal(typeof(EmptyQueryViewParameters), request.ViewParametersType);
    }

    [Fact]
    public void QueryRequestOfT_ExposesTypedAndUntypedParameters()
    {
        var parameters = new TestParameters { Category = "Alpha" };
        IQueryRequest request = CreateSut(parameters, new QueryBody(SearchText: "alpha"));

        Assert.Same(parameters, ((QueryRequest<TestParameters>)request).ViewParameters);
        Assert.Same(parameters, request.ViewParameters);
        Assert.Equal(typeof(TestParameters), request.ViewParametersType);
        Assert.Equal("alpha", request.Query!.SearchText);
    }

    [Fact]
    public void QueryFilterNodeCreateCondition_CreatesConditionNode()
    {
        var node = QueryFilterNode.CreateCondition("Category", FilterOperator.Equals, "Alpha");

        Assert.NotNull(node.Condition);
        Assert.Null(node.Group);
        Assert.Equal("Category", node.Condition!.Field);
        Assert.Equal(FilterOperator.Equals, node.Condition.Operator);
        Assert.Equal("Alpha", node.Condition.Values.Single());
    }

    [Fact]
    public void QueryFilterNodeCreateGroup_CreatesGroupNode()
    {
        var child = QueryFilterNode.CreateCondition("Category", FilterOperator.Equals, "Alpha");
        var node = QueryFilterNode.CreateGroup(LogicalOperator.And, child);

        Assert.Null(node.Condition);
        Assert.NotNull(node.Group);
        Assert.Equal(LogicalOperator.And, node.Group!.Operator);
        Assert.Single(node.Group.Filters);
    }

    [Fact]
    public void QueryResult_PreservesConstructorValues()
    {
        var record = new TestRecord();
        var result = new QueryResult<TestRecord>(2, 3, 4, [record]);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(3, result.Offset);
        Assert.Equal(4, result.PageSize);
        Assert.Same(record, result.Results.Single());
    }

    [Fact]
    public void KaleidoValidationException_CreatesWithCodeAndMessage()
    {
        var exception = new KaleidoValidationException(
            QueryableErrorCodes.InvalidField,
            "Field 'test-field' does not exist.");

        Assert.Equal(QueryableErrorCodes.InvalidField, exception.Code);
        Assert.Equal("Field 'test-field' does not exist.", exception.Message);
    }

    [Fact]
    public void KaleidoValidationException_CreatesWithCodeMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new KaleidoValidationException(
            QueryableErrorCodes.InvalidFilterValue,
            "Bad filter value.",
            inner);

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);
        Assert.Equal("Bad filter value.", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    private sealed class TestParameters
    {
        public string Category { get; init; } = string.Empty;
    }

    private sealed class TestRecord
    {
    }
}

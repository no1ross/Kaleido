namespace Kaleido.Queryable.Abstractions.UnitTests;

public sealed class QueryRequestTests
{
    [Fact]
    public void QueryRequest_UsesEmptyQueryViewParametersByDefault()
    {
        IQueryRequest request = new QueryRequest();

        Assert.IsType<EmptyQueryViewParameters>(request.ViewParameters);
        Assert.Equal(typeof(EmptyQueryViewParameters), request.ViewParametersType);
    }

    [Fact]
    public void QueryRequestOfT_ExposesTypedAndUntypedParameters()
    {
        var parameters = new TestParameters { Category = "Alpha" };
        IQueryRequest request = new QueryRequest<TestParameters>(parameters, new QueryBody(SearchText: "alpha"));

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

    private sealed class TestParameters
    {
        public string Category { get; init; } = string.Empty;
    }

    private sealed class TestRecord
    {
    }
}

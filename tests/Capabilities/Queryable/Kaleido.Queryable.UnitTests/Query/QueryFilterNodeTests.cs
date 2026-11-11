using Kaleido.UnitTests;

namespace Kaleido.Queryable.Query.UnitTests;

public sealed class QueryFilterNodeTests
    : SutFixture
{
    private static QueryFilterNode CreateSut() => QueryFilterNode.CreateCondition("ward", FilterOperator.In, "A", "B");

    [Fact]
    public void CreateCondition_IsAConditionNode_WithTheFieldOperatorAndValues()
    {
        var node = CreateSut();

        Assert.Null(node.Group);
        Assert.Equal("ward", node.Condition?.Field);
        Assert.Equal(FilterOperator.In, node.Condition?.Operator);
        Assert.Equal(["A", "B"], node.Condition?.Values ?? []);
    }

    [Fact]
    public void CreateCondition_CreatesTheNamedFieldAndValue()
    {
        var node = QueryFilterNode.CreateCondition("Category", FilterOperator.Equals, "Alpha");

        Assert.NotNull(node.Condition);
        Assert.Null(node.Group);
        Assert.Equal("Category", node.Condition.Field);
        Assert.Equal(FilterOperator.Equals, node.Condition.Operator);
        Assert.Equal("Alpha", node.Condition.Values.Single());
    }

    [Fact]
    public void CreateGroup_CreatesAGroupOfItsChild()
    {
        var child = QueryFilterNode.CreateCondition("Category", FilterOperator.Equals, "Alpha");
        var node = QueryFilterNode.CreateGroup(LogicalOperator.And, child);

        Assert.Null(node.Condition);
        Assert.NotNull(node.Group);
        Assert.Equal(LogicalOperator.And, node.Group.Operator);
        Assert.Single(node.Group.Filters);
    }

    [Fact]
    public void CreateGroup_IsAGroupNode_WithTheOperatorAndFiltersInOrder()
    {
        var first = CreateSut();
        var second = QueryFilterNode.CreateCondition("age", FilterOperator.GreaterThanOrEqual, 65);

        var node = QueryFilterNode.CreateGroup(LogicalOperator.Or, first, second);

        Assert.Null(node.Condition);
        Assert.Equal(LogicalOperator.Or, node.Group?.Operator);
        Assert.Equal([first, second], node.Group?.Filters ?? []);
    }
}

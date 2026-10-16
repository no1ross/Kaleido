using System.Text.Json;
using Kaleido.Queryable;
using Kaleido.Queryable.Query;

namespace Kaleido.Http.Queryable.UnitTests;

public sealed class QueryApiBodyExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void ToApiBody_WhenBodyIsNull_ReturnsNull()
    {
        QueryBody? body = null;

        Assert.Null(body.ToApiBody());
    }

    [Fact]
    public void ToApiBody_WhenSortAndPageAreSet_MapsThemToTheWireShape()
    {
        var body = new QueryBody(
            SearchText: "smith",
            Sort: [new QuerySort("lastName", SortDirection.Descending, 2)],
            Page: new QueryPage(Size: 25, Offset: 50));

        var api = body.ToApiBody();

        Assert.NotNull(api);
        Assert.Equal("smith", api.SearchText);
        var sort = Assert.Single(api.Sort!);
        Assert.Equal("lastName", sort.Field);
        Assert.Equal("Descending", sort.Direction);
        Assert.Equal(2, sort.Sequence);
        Assert.Equal(25, api.Page!.Size);
        Assert.Equal(50, api.Page.Offset);
    }

    [Fact]
    public void ToApiBody_WhenSortAndPageAreAbsent_LeavesThemNull()
    {
        var api = new QueryBody(SearchText: "x").ToApiBody();

        Assert.NotNull(api);
        Assert.Null(api.Sort);
        Assert.Null(api.Page);
        Assert.Null(api.Filter);
    }

    [Fact]
    public void ToApiBody_WhenFilterIsACondition_SerializesItsValues()
    {
        var body = new QueryBody(
            Filter: QueryFilterNode.CreateCondition("age", FilterOperator.Equals, 42, "x"));

        var condition = body.ToApiBody()!.Filter!.Condition;

        Assert.NotNull(condition);
        Assert.Equal("age", condition.Field);
        Assert.Equal("Equals", condition.Operator);
        Assert.Collection(
            condition.Values!,
            x => Assert.Equal(42, x.GetInt32()),
            x => Assert.Equal("x", x.GetString()));
    }

    [Fact]
    public void ToApiBody_WhenFilterIsAGroup_MapsItsNestedFilters()
    {
        var body = new QueryBody(
            Filter: QueryFilterNode.CreateGroup(
                LogicalOperator.Or,
                QueryFilterNode.CreateCondition("state", FilterOperator.Equals, "CO"),
                QueryFilterNode.CreateCondition("state", FilterOperator.Equals, "UT")));

        var group = body.ToApiBody()!.Filter!.Group;

        Assert.NotNull(group);
        Assert.Equal("Or", group.Operator);
        Assert.Equal(2, group.Filters!.Count);
        Assert.All(group.Filters, x => Assert.Equal(JsonValueKind.String, x.Condition!.Values![0].ValueKind));
    }

    [Fact]
    public void ToApiBody_WhenConditionValuesAndGroupFiltersAreMissing_LeavesThemNull()
    {
        var condition = new QueryBody(
            Filter: new QueryFilterNode(new QueryFilterCondition("deletedAt", FilterOperator.IsNull, null!), null));
        var group = new QueryBody(
            Filter: new QueryFilterNode(null, new QueryFilterGroup(LogicalOperator.And, null!)));

        var apiCondition = condition.ToApiBody()!.Filter!.Condition;
        var apiGroup = group.ToApiBody()!.Filter!.Group;

        Assert.NotNull(apiCondition);
        Assert.Equal("IsNull", apiCondition.Operator);
        Assert.Null(apiCondition.Values);
        Assert.NotNull(apiGroup);
        Assert.Equal("And", apiGroup.Operator);
        Assert.Null(apiGroup.Filters);
    }

    [Fact]
    public void ToApiBody_WhenFilterNodeIsEmpty_DropsIt()
    {
        var body = new QueryBody(Filter: new QueryFilterNode(null, null));

        Assert.Null(body.ToApiBody()!.Filter);
    }
}

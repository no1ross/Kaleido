namespace Kaleido.Queryable.Query;

/// <summary>
/// A node of a filter tree: either a single <see cref="QueryFilterCondition"/> or a
/// <see cref="QueryFilterGroup"/> of child nodes. Use <see cref="CreateCondition"/> and
/// <see cref="CreateGroup"/> to build one.
/// </summary>
/// <remarks>
/// Exactly one of <c>Condition</c> and <c>Group</c> must be set; both or neither fails with
/// <see cref="QueryableErrorCodes.InvalidFilterNode"/>. Groups may be nested up to ten levels
/// below the root (<see cref="QueryableErrorCodes.FilterDepthExceeded"/>).
/// </remarks>
/// <example>
/// <code>
/// var filter = QueryFilterNode.CreateGroup(
///     LogicalOperator.And,
///     QueryFilterNode.CreateCondition("Status", FilterOperator.In, "Open", "Pending"),
///     QueryFilterNode.CreateCondition("Amount", FilterOperator.Between, 100m, 500m));
/// </code>
/// </example>
/// <param name="Condition">The condition this node tests, or <see langword="null"/> when it is a group.</param>
/// <param name="Group">The group this node combines, or <see langword="null"/> when it is a condition.</param>
public sealed record QueryFilterNode
(
    QueryFilterCondition? Condition,
    QueryFilterGroup? Group
)
{
    /// <summary>Creates a node that tests a single field.</summary>
    /// <param name="field">The query context field to test (matched case-insensitively).</param>
    /// <param name="operator">The operator; it must be one the field declares as filterable.</param>
    /// <param name="values">The operator's values; see <see cref="QueryFilterCondition"/>.</param>
    /// <returns>A node with <c>Condition</c> set and no <c>Group</c>.</returns>
    public static QueryFilterNode CreateCondition(
        string field,
        FilterOperator @operator,
        params object?[] values)
    {
        return new(
            new QueryFilterCondition(
                field,
                @operator,
                values),
                Group: null);
    }

    /// <summary>Creates a node that combines child nodes with a logical operator.</summary>
    /// <param name="operator">How the children are combined.</param>
    /// <param name="filters">The child nodes; at least one is required (<see cref="QueryableErrorCodes.EmptyFilterGroup"/>).</param>
    /// <returns>A node with <c>Group</c> set and no <c>Condition</c>.</returns>
    public static QueryFilterNode CreateGroup(
        LogicalOperator @operator,
        params QueryFilterNode[] filters)
    {
        return new(
        Condition: null,
            new QueryFilterGroup(
                @operator,
                filters.ToList()));
    }
}

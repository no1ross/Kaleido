namespace Kaleido.Queryable.Query;

#region Filters

/// <summary>
/// A test of one query context field: the field, the operator, and the operator's values.
/// </summary>
/// <remarks>
/// The field must exist (<see cref="QueryableErrorCodes.InvalidField"/>), be filterable
/// (<see cref="QueryableErrorCodes.FieldNotFilterable"/>) and declare the operator in its
/// <see cref="FilterableAttribute"/> (<see cref="QueryableErrorCodes.UnsupportedOperator"/>).
/// </remarks>
/// <param name="Field">
/// The query context field name, matched case-insensitively. Empty or whitespace fails with
/// <see cref="QueryableErrorCodes.MissingFilterField"/>.
/// </param>
/// <param name="Operator">The operator to apply; see <see cref="FilterOperator"/>.</param>
/// <param name="Values">
/// The operator's values. Comparison, equality and string operators use the first value;
/// <see cref="FilterOperator.In"/> and <see cref="FilterOperator.NotIn"/> use all of them;
/// <see cref="FilterOperator.Between"/> and <see cref="FilterOperator.NotBetween"/> use the first
/// two as lower and upper bound; the null and boolean operators use none. Non-null values must be
/// of a supported runtime type (<see cref="QueryableErrorCodes.UnsupportedRuntimeType"/>). When
/// Kaleido applies the filter to a local source, a missing required value fails with
/// <see cref="QueryableErrorCodes.InvalidFilterNode"/> and a value that cannot be converted to the
/// field's type with <see cref="QueryableErrorCodes.InvalidFilterValue"/>.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record QueryFilterCondition
(
    string Field,
    FilterOperator Operator,
    IReadOnlyList<object?> Values
);

/// <summary>
/// A group of filter nodes combined with one <see cref="LogicalOperator"/>.
/// </summary>
/// <param name="Operator">
/// <see cref="LogicalOperator.And"/> matches when every child matches;
/// <see cref="LogicalOperator.Or"/> when any child matches.
/// </param>
/// <param name="Filters">
/// The child nodes. An empty group fails with <see cref="QueryableErrorCodes.EmptyFilterGroup"/>.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record QueryFilterGroup
(
    LogicalOperator Operator,
    IReadOnlyList<QueryFilterNode> Filters
);

#endregion

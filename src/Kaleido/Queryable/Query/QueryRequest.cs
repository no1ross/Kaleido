namespace Kaleido.Queryable.Query;

public interface IQueryRequest<TParameters> : IQueryRequest
    where TParameters : class
{
    new TParameters? ViewParameters { get; }
}

public interface IQueryRequest
{
    QueryBody? Query { get; }
    object? ViewParameters { get; }
    Type ViewParametersType { get; }
}

[ExcludeFromCodeCoverage]
public sealed record QueryRequest(
    QueryBody? Query = null)
    : QueryRequest<EmptyQueryViewParameters>(
        ViewParameters: new EmptyQueryViewParameters(),
        Query: Query);

[ExcludeFromCodeCoverage]
public record QueryRequest<TParameters>(
    TParameters? ViewParameters,
    QueryBody? Query = null)
    : IQueryRequest<TParameters>
    where TParameters : class
{
    object? IQueryRequest.ViewParameters =>
        ViewParameters;

    Type IQueryRequest.ViewParametersType =>
        typeof(TParameters);
}

[ExcludeFromCodeCoverage]
public record QueryBody
(
    string? SearchText = null,
    QueryFilterNode? Filter = null,
    IReadOnlyList<QuerySort>? Sort = null,
    QueryPage? Page = null
);

#region Filters

[ExcludeFromCodeCoverage]
public sealed record QueryFilterNode
(
    QueryFilterCondition? Condition,
    QueryFilterGroup? Group
)
{
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

[ExcludeFromCodeCoverage]
public sealed record QueryFilterCondition
(
    string Field,
    FilterOperator Operator,
    IReadOnlyList<object?> Values
);

[ExcludeFromCodeCoverage]
public sealed record QueryFilterGroup
(
    LogicalOperator Operator,
    IReadOnlyList<QueryFilterNode> Filters
);

#endregion

#region Sort/Page

[ExcludeFromCodeCoverage]
public record QuerySort(
    string Field,
    SortDirection Direction,
    int? Sequence = null);

/// <summary>
/// The page of results a query asks for. Paging is accepted only for a source or view declared
/// <see cref="PageableAttribute"/>; otherwise the request fails with
/// <see cref="QueryableErrorCodes.PagingNotSupported"/>.
/// </summary>
/// <remarks>
/// A present page is an explicit page even when both values are omitted (for example
/// <c>{"page":{}}</c>), which selects the explicit-page <c>TotalCount</c> behaviour; an absent
/// page does not.
/// </remarks>
/// <param name="Size">
/// Results per page. Omitted (<see langword="null"/>) uses the capability's
/// <see cref="PageableAttribute.DefaultSize"/>. A supplied value must be greater than zero and no
/// greater than <see cref="PageableAttribute.MaxSize"/>
/// (<see cref="QueryableErrorCodes.InvalidPageSize"/>).
/// </param>
/// <param name="Offset">
/// Zero-based result offset. Omitted (<see langword="null"/>) means zero; a negative value fails
/// with <see cref="QueryableErrorCodes.InvalidPageOffset"/>.
/// </param>
[ExcludeFromCodeCoverage]
public record QueryPage(
    int? Size,
    int? Offset);

#endregion

[ExcludeFromCodeCoverage]
public sealed record QueryResult<TView>(int TotalCount, int Offset, int PageSize, IReadOnlyCollection<TView> Results)
    where TView : class;

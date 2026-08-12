using Kaleido.Queryable.Metadata;

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

public sealed record QueryRequest(
    QueryBody? Query = null)
    : QueryRequest<EmptyQueryViewParameters>(
        ViewParameters: new EmptyQueryViewParameters(),
        Query: Query);

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

public record QueryBody
(
    string? SearchText = null,
    QueryFilterNode? Filter = null,
    IReadOnlyList<QuerySort>? Sort = null,
    QueryPage? Page = null
);

#region Filters

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
            null);
    }

    public static QueryFilterNode CreateGroup(
        LogicalOperator @operator,
        params QueryFilterNode[] filters)
    {
        return new(
            null,
            new QueryFilterGroup(
                @operator,
                filters.ToList()));
    }
}

public sealed record QueryFilterCondition
(
    string Field,
    FilterOperator Operator,
    IReadOnlyList<object?> Values
);

public sealed record QueryFilterGroup
(
    LogicalOperator Operator,
    IReadOnlyList<QueryFilterNode> Filters
);

#endregion

#region Sort/Page

public record QuerySort(
    string Field,
    SortDirection Direction,
    int? Sequence = null);

public record QueryPage(
    int? Size,
    int? Offset);

#endregion

public sealed record QueryResult<TRecord>(int TotalCount, int Offset, int PageSize, IReadOnlyCollection<TRecord> Records)
    where TRecord : class;

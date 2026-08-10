using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

/// <summary>Value-set query request.</summary>
public record QueryRequest
(
    NamedQuery? NamedQuery = null,
    QueryBody? Query = null
);

public record QueryBody
(
    string? SearchText = null,
    QueryFilterNode? Filter = null,
    IReadOnlyList<QuerySort>? Sort = null,
    QueryPage? Page = null
);

public record NamedQuery
(
    string Name,
    IReadOnlyDictionary<string, object?>? Parameters = null
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

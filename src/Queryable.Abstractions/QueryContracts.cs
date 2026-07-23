using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable;

/// <summary>Value-set query request.</summary>
public record KaleidoQueryRequest
(
    string? QueryName,
    KaleidoQueryBody? Query,
    IReadOnlyDictionary<string, object?>? Parameters = null
);

public record KaleidoQueryBody
(
    QuerySearchNode? Search,
    QueryFilterNode? Filter,
    IReadOnlyList<QuerySort>? Sort,
    QueryPage? Page
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

    public void Validate()
    {
        if ((Condition is null) == (Group is null))
        {
            throw new InvalidOperationException(
                "Exactly one of Condition or Group must be specified.");
        }
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

#region Search

public sealed record QuerySearchNode
(
    QuerySearchCondition? Condition,
    QuerySearchGroup? Group
)
{
    public static QuerySearchNode CreateCondition(
        string searchText,
        MatchMode matchMode,
        string? field = null)
    {
        return new(
            new QuerySearchCondition(
                searchText,
                matchMode,
                field),
            null);
    }

    public static QuerySearchNode CreateGroup(
        LogicalOperator @operator,
        params QuerySearchNode[] searches)
    {
        return new(
            null,
            new QuerySearchGroup(
                @operator,
                searches.ToList()));
    }

    public void Validate()
    {
        if ((Condition is null) == (Group is null))
        {
            throw new InvalidOperationException(
                "Exactly one of Condition or Group must be specified.");
        }
    }
}

public sealed record QuerySearchCondition
(
    string SearchText,
    MatchMode MatchMode,
    string? Field = null
);

public sealed record QuerySearchGroup
(
    LogicalOperator Operator,
    IReadOnlyList<QuerySearchNode> Searches
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

public sealed record QueryResult<TRecord>(IReadOnlyList<TRecord> Items, int TotalCount, RuntimeRecordMetadata RuntimeMetadata)
    where TRecord : class;

public sealed record KaleidoQueryResponse<TRecord>(
    RecordDescriptor Descriptor,
    int TotalCount,
    IReadOnlyList<TRecord> Items)
    where TRecord : class;

public static class QueryFilter
{
    public static QueryFilterNode Eq(
        string field,
        object? value)
    {
        return QueryFilterNode.CreateCondition(
            field,
            FilterOperator.Eq,
            value);
    }

    public static QueryFilterNode Group(
        LogicalOperator op,
        params QueryFilterNode[] filters)
    {
        return QueryFilterNode.CreateGroup(
            op,
            filters);
    }
}
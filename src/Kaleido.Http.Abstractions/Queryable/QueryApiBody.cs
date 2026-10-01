using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Http.Queryable;

/// <summary>
/// Converts a runtime <see cref="QueryBody"/> to the transport-level
/// <see cref="QueryApiBody"/> shape. Used when a core caller (e.g. a delegated
/// view source) forwards a query over HTTP.
/// </summary>
public static class QueryApiBodyExtensions
{
    /// <summary>
    /// Converts <paramref name="body"/> to <see cref="QueryApiBody"/>.
    /// Returns null when <paramref name="body"/> is null.
    /// </summary>
    public static QueryApiBody? ToApiBody(this QueryBody? body)
    {
        if (body is null)
        {
            return null;
        }

        return new QueryApiBody
        {
            SearchText = body.SearchText,
            Filter = ToApiFilterNode(body.Filter),
            Sort = body.Sort?
                .Select(x => new QueryApiSort
                {
                    Field = x.Field,
                    Direction = x.Direction.ToString(),
                    Sequence = x.Sequence
                })
                .ToArray(),
            Page = body.Page is null
                ? null
                : new QueryApiPage { Size = body.Page.Size, Offset = body.Page.Offset }
        };
    }

    private static QueryApiFilterNode? ToApiFilterNode(QueryFilterNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node.Condition is not null)
        {
            return new QueryApiFilterNode
            {
                Condition = new QueryApiFilterCondition
                {
                    Field = node.Condition.Field,
                    Operator = node.Condition.Operator.ToString(),
                    Values = node.Condition.Values?
                        .Select(v => JsonSerializer.SerializeToElement(v))
                        .ToArray()
                }
            };
        }

        if (node.Group is not null)
        {
            return new QueryApiFilterNode
            {
                Group = new QueryApiFilterGroup
                {
                    Operator = node.Group.Operator.ToString(),
                    Filters = node.Group.Filters?
                        .Select(ToApiFilterNode)
                        .OfType<QueryApiFilterNode>()
                        .ToArray()
                }
            };
        }

        return null;
    }
}

// ── Query request body ────────────────────────────────────────────────────────

[ExcludeFromCodeCoverage]
public record QueryApiBody
{
    /// <summary>Free-text search across all searchable fields.</summary>
    [JsonPropertyName("searchText")]
    public string? SearchText { get; init; }

    /// <summary>Filter expression tree. Cannot coexist with itself.</summary>
    [JsonPropertyName("filter")]
    public QueryApiFilterNode? Filter { get; init; }

    /// <summary>Sort clauses, applied in order.</summary>
    [JsonPropertyName("sort")]
    public IReadOnlyList<QueryApiSort>? Sort { get; init; }

    /// <summary>Page bounds.</summary>
    [JsonPropertyName("page")]
    public QueryApiPage? Page { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record QueryApiFilterNode
{
    /// <summary>A leaf condition. Mutually exclusive with Group.</summary>
    [JsonPropertyName("condition")]
    public QueryApiFilterCondition? Condition { get; init; }

    /// <summary>A logical group of child nodes. Mutually exclusive with Condition.</summary>
    [JsonPropertyName("group")]
    public QueryApiFilterGroup? Group { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record QueryApiFilterCondition
{
    /// <summary>The field name to filter on.</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    /// <summary>
    /// The filter operator. Accepted values match FilterOperator names,
    /// case-insensitive: "equals", "notequals", "greaterthan", "lessthan",
    /// "greaterthanorequal", "lessthanorequal", "contains", "notcontains",
    /// "startswith", "endswith", "in", "notin", "between", "notbetween",
    /// "isnull", "isnotnull", "istrue", "isfalse".
    /// </summary>
    [JsonPropertyName("operator")]
    public required string Operator { get; init; }

    /// <summary>The values to compare against. JsonElement is used for raw transport.</summary>
    [JsonPropertyName("values")]
    public IReadOnlyList<JsonElement>? Values { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record QueryApiFilterGroup
{
    /// <summary>
    /// The logical operator joining child filters: "and" or "or" (case-insensitive).
    /// </summary>
    [JsonPropertyName("operator")]
    public required string Operator { get; init; }

    /// <summary>Child filter nodes.</summary>
    [JsonPropertyName("filters")]
    public IReadOnlyList<QueryApiFilterNode>? Filters { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record QueryApiSort
{
    /// <summary>The field to sort by.</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    /// <summary>Sort direction: "ascending" or "descending" (case-insensitive).</summary>
    [JsonPropertyName("direction")]
    public required string Direction { get; init; }

    /// <summary>Optional explicit sort sequence. Defaults to positional order.</summary>
    [JsonPropertyName("sequence")]
    public int? Sequence { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record QueryApiPage
{
    /// <summary>Number of results per page.</summary>
    [JsonPropertyName("size")]
    public int? Size { get; init; }

    /// <summary>Zero-based result offset.</summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; init; }
}

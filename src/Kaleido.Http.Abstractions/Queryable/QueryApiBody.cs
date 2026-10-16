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
                    Sequence = x.Sequence,
                })
                .ToArray(),
            Page = body.Page is null
                ? null
                : new QueryApiPage { Size = body.Page.Size, Offset = body.Page.Offset },
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
                    // The Kaleido wire format, like every other value Kaleido sends: an enum as its
                    // camelCase name, never a number the receiving service would have to guess at.
                    Values = node.Condition.Values?
                        .Select(v => JsonSerializer.SerializeToElement(v, KaleidoJsonOptions.Options))
                        .ToArray(),
                },
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
                        .ToArray(),
                },
            };
        }

        return null;
    }
}

// ── Query request body ────────────────────────────────────────────────────────

/// <summary>
/// The wire form of a query (the <c>query</c> member of a <c>QueryApiRequest</c>): free-text search,
/// a filter tree, sort clauses and a page. Every member is optional; enum-like values (operators,
/// sort direction) are strings so the body stays transport-neutral.
/// </summary>
/// <remarks>
/// The HTTP transport converts the body to the runtime query, resolving operator and direction names
/// case-insensitively and coercing filter values to each field's type; an unknown name or an
/// unconvertible value is rejected as a validation error (<c>qry_invalid_filter_value</c>).
/// </remarks>
[ExcludeFromCodeCoverage]
public record QueryApiBody
{
    /// <summary>Free-text search across all searchable fields.</summary>
    [JsonPropertyName("searchText")]
    public string? SearchText { get; init; }

    /// <summary>The root of the filter expression tree: a single condition or a group of nodes. Null applies no filter.</summary>
    [JsonPropertyName("filter")]
    public QueryApiFilterNode? Filter { get; init; }

    /// <summary>Sort clauses, applied in order.</summary>
    [JsonPropertyName("sort")]
    public IReadOnlyList<QueryApiSort>? Sort { get; init; }

    /// <summary>Page bounds.</summary>
    [JsonPropertyName("page")]
    public QueryApiPage? Page { get; init; }
}

/// <summary>
/// A node of the filter tree: set exactly one of <see cref="Condition"/> or <see cref="Group"/>.
/// </summary>
/// <remarks>
/// A node with both, or neither, is rejected as a validation error (<c>qry_invalid_filter_node</c>).
/// </remarks>
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

/// <summary>
/// A leaf filter: compares a field with zero or more values using an operator, for example
/// <c>{"field":"status","operator":"in","values":["open","pending"]}</c>.
/// </summary>
/// <remarks>
/// The field must be a filterable field of the queried source that allows the operator; the HTTP
/// transport coerces each value to that field's type.
/// </remarks>
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

/// <summary>A logical group of filter nodes joined by <c>and</c> or <c>or</c>; groups may nest.</summary>
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

/// <summary>A sort clause: a sortable field and a direction.</summary>
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

/// <summary>
/// Wire form of the requested page (<c>{"size":10,"offset":0}</c>). A present page is an explicit
/// page even with both members omitted (<c>{}</c>); it is accepted only for a <c>[Pageable]</c>
/// source or view (otherwise <c>qry_paging_not_supported</c>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record QueryApiPage
{
    /// <summary>
    /// Number of results per page. Omitted uses the capability's configured <c>DefaultSize</c>; a
    /// supplied value must be greater than zero and no greater than <c>MaxSize</c>
    /// (<c>qry_invalid_page_size</c>).
    /// </summary>
    [JsonPropertyName("size")]
    public int? Size { get; init; }

    /// <summary>
    /// Zero-based result offset. Omitted means zero; a negative value is rejected
    /// (<c>qry_invalid_page_offset</c>).
    /// </summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; init; }
}

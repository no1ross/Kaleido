using System.Text.Json;

namespace Kaleido.Queryable.Http;

/// <summary>
/// Converts a runtime <see cref="QueryBody"/> to the transport-level
/// <see cref="QueryApiBody"/> shape. Used when a caller (e.g. a delegated
/// view source) forwards a query over HTTP.
/// </summary>
public static class QueryApiBodyExtensions
{
    /// <summary>
    /// Converts <paramref name="body"/> to the HTTP request's Queryable body.
    /// </summary>
    /// <param name="body">A runtime query with CLR filter values, or <see langword="null"/>.</param>
    /// <returns>A transport body with JSON values and named operators/directions, or
    /// <see langword="null"/> when no query was supplied.</returns>
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

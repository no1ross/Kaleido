using Kaleido.Json;
using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.AspNetCore;

internal static class QueryableValueNormalizer
{
    public static IReadOnlyDictionary<string, object?>? Normalize(
        IReadOnlyDictionary<string, object?>? values,
        IReadOnlyCollection<QueryParameterMetadata>? parameters)
    {
        if (values is null)
        {
            return null;
        }

        if (parameters is null ||
            parameters.Count == 0)
        {
            return values;
        }

        var result =
            new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in parameters)
        {
            if (!values.TryGetValue(
                    parameter.Name,
                    out var value))
            {
                continue;
            }

            try
            {
                result[parameter.Name] =
                    ValueConverter.Convert(
                        value,
                        parameter.Type);
            }
            catch (Exception)
            {
                throw new InvalidParameterValueException(
                    parameter.Name,
                    value,
                    parameter.Type);
            }
        }

        return result;
    }

    public static QueryBody? Normalize(
        QueryBody? query,
        RecordMetadata metadata)
    {
        if (query is null)
        {
            return null;
        }

        return query with
        {
            Filter = NormalizeFilter(
                query.Filter,
                metadata)
        };
    }

    private static QueryFilterNode? NormalizeFilter(
        QueryFilterNode? node,
        RecordMetadata metadata)
    {
        if (node is null)
        {
            return null;
        }

        if (node.Condition is not null)
        {
            return node with
            {
                Condition = NormalizeCondition(
                    node.Condition,
                    metadata)
            };
        }

        if (node.Group is not null)
        {
            return node with
            {
                Group = new QueryFilterGroup(
                    node.Group.Operator,
                    node.Group.Filters
                        .Select(x =>
                            NormalizeFilter(
                                x,
                                metadata)!)
                        .ToArray())
            };
        }

        return node;
    }

    private static QueryFilterCondition NormalizeCondition(
        QueryFilterCondition condition,
        RecordMetadata metadata)
    {
        var field =
            metadata.Fields.SingleOrDefault(x =>
                string.Equals(
                    x.Name,
                    condition.Field,
                    StringComparison.OrdinalIgnoreCase));

        if (field is null)
        {
            throw new InvalidFieldException(
                condition.Field,
                metadata.Name);
        }

        try
        {
            var values =
                condition.Values
                    .Select(x =>
                        x is null
                            ? null
                            : ValueConverter.Convert(
                                x,
                                field.FieldType))
                    .ToArray();

            return condition with
            {
                Values = values
            };
        }
        catch (Exception)
        {
            throw new InvalidFilterValueException(
                condition.Field,
                condition.Values.FirstOrDefault(),
                field.FieldType);
        }
    }
}

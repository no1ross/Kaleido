using Kaleido.Json;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;

namespace Kaleido.Http.Queryable;

internal static class QueryableValueNormalizer
{
    public static QueryBody? Normalize(
        QueryBody? query,
        QueryContextMetadata metadata)
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
        QueryContextMetadata metadata)
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
        QueryContextMetadata metadata)
    {
        var field =
            metadata.Fields.SingleOrDefault(x =>
                string.Equals(
                    x.Name,
                    condition.Field,
                    StringComparison.OrdinalIgnoreCase));

        if (field is null)
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryInvalidField,
                $"Field '{condition.Field}' does not exist on record '{metadata.Name}'.");
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryInvalidFilterValue,
                $"Value '{condition.Values.FirstOrDefault()}' is not valid for field '{condition.Field}'. Expected a value of type '{field.FieldType.Name}'.",
                exception);
        }
    }
}

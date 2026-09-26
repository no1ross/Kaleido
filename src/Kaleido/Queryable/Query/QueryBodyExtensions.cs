using Kaleido.Json;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

internal static class QueryBodyExtensions
{
    public static QueryBody? Normalize(
        this QueryBody? query,
        IValueConverter valueConverter,
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
                valueConverter,
                metadata)
        };
    }

    private static QueryFilterNode? NormalizeFilter(
        QueryFilterNode? node,
        IValueConverter valueConverter,
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
                    valueConverter,
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
                                valueConverter,
                                metadata))
                        .OfType<QueryFilterNode>()
                        .ToArray())
            };
        }

        return node;
    }

    private static QueryFilterCondition NormalizeCondition(
        QueryFilterCondition condition,
        IValueConverter valueConverter,
        QueryContextMetadata metadata)
    {
        var field =
            metadata.GetField(condition.Field);

        try
        {
            var values =
                condition.Values
                    .Select(x =>
                        x is null
                            ? null
                            : valueConverter.Convert(
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

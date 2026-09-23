using Kaleido.Json;

namespace Kaleido.Http.Queryable;

internal sealed class QueryableValueNormalizer(
    IValueConverter valueConverter)
{
    public QueryBody? Normalize(
        QueryBody? query,
        QueryContextMetadata metadata)
    {
        if (query is null)
        {
            return null;
        }

        var fields = new FieldLookup(metadata);

        return query with
        {
            Filter = NormalizeFilter(
                query.Filter,
                fields)
        };
    }

    private QueryFilterNode? NormalizeFilter(
        QueryFilterNode? node,
        FieldLookup fields)
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
                    fields)
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
                                fields))
                        .OfType<QueryFilterNode>()
                        .ToArray())
            };
        }

        return node;
    }

    private QueryFilterCondition NormalizeCondition(
        QueryFilterCondition condition,
        FieldLookup fields)
    {
        var field =
            fields.Get(condition.Field);

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

    private sealed class FieldLookup(
        QueryContextMetadata metadata)
    {
        private readonly Dictionary<string, FieldMetadata> _byName =
            metadata.Fields.ToDictionary(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase);

        public QueryContextMetadata Metadata { get; } = metadata;

        public FieldMetadata Get(
            string name) =>
            _byName.TryGetValue(name, out var field)
                ? field
                : throw new KaleidoValidationException(
                    ValidationErrorCodes.QryInvalidField,
                    $"Field '{name}' does not exist on record '{Metadata.Name}'.");
    }
}

using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

internal sealed class QueryRequestValidator : IRecordQueryValidator
{
    public void Validate(
        QueryRequest request,
        RecordRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);

        ValidateNamedQuery(
            request,
            registration);

        ValidateFilter(
            request.Query?.Filter,
            registration.Metadata);

        ValidateSearch(
            request.Query?.Search,
            registration.Metadata);

        ValidateSort(
            request.Query?.Sort,
            registration.Metadata);

        ValidatePage(
            request.Query?.Page,
            registration.Metadata);
    }

    private static void ValidateNamedQuery(
        QueryRequest request,
        RecordRegistration registration)
    {
        if (request.NamedQuery is null ||
            string.IsNullOrWhiteSpace(request.NamedQuery.Name))
        {
            return;
        }

        var allowed =
            registration.NamedQueryTypes.SingleOrDefault(
                x => string.Equals(
                    x.Metadata.Name,
                    request.NamedQuery.Name,
                    StringComparison.OrdinalIgnoreCase));

        if (allowed is null)
        {
            throw new InvalidOperationException(
                $"Named query '{request.NamedQuery.Name}' is not allowed for record '{registration.Metadata.Name}'.");
        }

        foreach (var parameter in allowed.Metadata.Parameters ?? [])
        {
            if (request.NamedQuery.Parameters is null ||
                !request.NamedQuery.Parameters.TryGetValue(
                    parameter.Name,
                    out var value) ||
                value is null)
            {
                if (parameter.Required)
                {
                    throw new InvalidOperationException(
                        $"Named query '{request.NamedQuery.Name}' requires parameter '{parameter.Name}'.");
                }

                continue;
            }

            ValidateParameterType(
                parameter,
                value);
        }
    }

    private static void ValidateFilterValueTypes(QueryFilterCondition condition)
    {
        foreach (var value in condition.Values)
        {
            if (value is null)
            {
                continue;
            }

            ValidateSupportedRuntimeType(
                condition.Field,
                value);
        }
    }

    private static void ValidateSupportedRuntimeType(
        string name,
        object value)
    {
        var actualType =
            Nullable.GetUnderlyingType(
                value.GetType())
            ?? value.GetType();

        if (DataTypeMapper.IsSupportedType(actualType))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Value '{name}' contains unsupported runtime type '{actualType.FullName}'. " +
            "Transport layers must normalize values before invoking Queryable.");
    }

    private static void ValidateParameterType(
        QueryParameterMetadata parameter,
        object value)
    {
        var expectedType =
            Nullable.GetUnderlyingType(parameter.Type)
            ?? parameter.Type;

        var actualType =
            value.GetType();

        if (expectedType.IsAssignableFrom(actualType))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Parameter '{parameter.Name}' expects values of type '{expectedType.Name}' but received '{actualType.Name}'.");
    }

    private static void ValidateFilter(
        QueryFilterNode? node,
        RecordMetadata metadata)
    {
        if (node is null)
        {
            return;
        }

        if (node.Condition is not null &&
            node.Group is not null)
        {
            throw new InvalidOperationException(
                "Filter node cannot specify both Condition and Group.");
        }

        if (node.Condition is not null)
        {
            ValidateFilterCondition(
                node.Condition,
                metadata);

            return;
        }

        if (node.Group is not null)
        {
            ValidateFilterGroup(
                node.Group,
                metadata);

            return;
        }

        throw new InvalidOperationException(
            "Filter node must specify either Condition or Group.");
    }

    private static void ValidateFilterGroup(
        QueryFilterGroup group,
        RecordMetadata metadata)
    {
        if (group.Filters.Count == 0)
        {
            throw new InvalidOperationException(
                "Filter group must contain at least one expression.");
        }

        foreach (var child in group.Filters)
        {
            ValidateFilter(
                child,
                metadata);
        }
    }

    private static void ValidateFilterCondition(
        QueryFilterCondition condition,
        RecordMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(condition.Field))
        {
            throw new InvalidOperationException(
                "Filter field is required.");
        }

        var field =
            GetField(
                metadata,
                condition.Field);

        if (!field.IsFilterable)
        {
            throw new InvalidOperationException(
                $"Field '{condition.Field}' is not filterable.");
        }

        if (!field.FilterOperators.Contains(condition.Operator))
        {
            throw new InvalidOperationException(
                $"Operator '{condition.Operator}' is not supported for field '{condition.Field}'.");
        }

        ValidateFilterValueTypes(condition);
    }

    private static void ValidateSearch(
        QuerySearchNode? node,
        RecordMetadata metadata)
    {
        if (node is null)
        {
            return;
        }

        if (node.Condition is not null &&
            node.Group is not null)
        {
            throw new InvalidOperationException(
                "Search node cannot specify both Condition and Group.");
        }

        if (node.Condition is not null)
        {
            ValidateSearchCondition(
                node.Condition,
                metadata);

            return;
        }

        if (node.Group is not null)
        {
            ValidateSearchGroup(
                node.Group,
                metadata);

            return;
        }

        throw new InvalidOperationException(
            "Search node must specify either Condition or Group.");
    }

    private static void ValidateSearchGroup(
        QuerySearchGroup group,
        RecordMetadata metadata)
    {
        if (group.Searches.Count == 0)
        {
            throw new InvalidOperationException(
                "Search group must contain at least one expression.");
        }

        foreach (var child in group.Searches)
        {
            ValidateSearch(
                child,
                metadata);
        }
    }

    private static void ValidateSearchCondition(
        QuerySearchCondition condition,
        RecordMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(condition.SearchText))
        {
            throw new InvalidOperationException(
                "Search text is required.");
        }

        var fields =
            metadata.Fields
                .Where(x => x.IsSearchable);

        if (!string.IsNullOrWhiteSpace(condition.Field))
        {
            fields =
                fields.Where(x =>
                    string.Equals(
                        x.Name,
                        condition.Field,
                        StringComparison.OrdinalIgnoreCase));
        }

        var list =
            fields.ToArray();

        if (list.Length == 0)
        {
            throw new InvalidOperationException(
                $"No searchable fields exist for search field '{condition.Field ?? "*"}'.");
        }

        if (!list.Any(x =>
                x.MatchModes.Contains(
                    condition.MatchMode)))
        {
            throw new InvalidOperationException(
                $"Match mode '{condition.MatchMode}' is not supported for search field '{condition.Field ?? "*"}'.");
        }
    }

    private static void ValidateSort(
        IReadOnlyList<QuerySort>? sorts,
        RecordMetadata metadata)
    {
        if (sorts is null)
        {
            return;
        }

        var duplicateFields =
            sorts
                .GroupBy(
                    x => x.Field,
                    StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToArray();

        if (duplicateFields.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate sort fields are not allowed: {string.Join(", ", duplicateFields)}.");
        }

        foreach (var sort in sorts)
        {
            var field =
                GetField(
                    metadata,
                    sort.Field);

            if (!field.IsSortable)
            {
                throw new InvalidOperationException(
                    $"Field '{sort.Field}' is not sortable.");
            }
        }
    }

    private static void ValidatePage(
        QueryPage? page,
        RecordMetadata metadata)
    {
        if (page is null)
        {
            return;
        }

        var pageable =
            metadata.Pageable
            ?? throw new InvalidOperationException(
                $"Record '{metadata.Name}' does not support paging.");

        if (page.Size is <= 0)
        {
            throw new InvalidOperationException(
                "Page size must be greater than zero.");
        }

        if (page.Size.HasValue &&
            page.Size.Value > pageable.MaxSize)
        {
            throw new InvalidOperationException(
                $"Page size '{page.Size.Value}' exceeds max page size '{pageable.MaxSize}'.");
        }
    }

    private static FieldMetadata GetField(
        RecordMetadata metadata,
        string name)
    {
        return metadata.Fields.SingleOrDefault(x =>
                   string.Equals(
                       x.Name,
                       name,
                       StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException(
                   $"Field '{name}' does not exist on record '{metadata.Name}'.");
    }
}
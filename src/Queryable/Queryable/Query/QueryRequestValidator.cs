using Kaleido.Queryable.Exceptions;
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
            throw new NamedQueryNotAllowedException(request.NamedQuery.Name, registration.Metadata.Name);
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
                    throw new NamedQueryRequiredException(request.NamedQuery.Name, parameter.Name);
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

        throw new UnsupportedRuntimeTypeException(name, actualType);
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

        throw new InvalidParameterTypeException(parameter.Name, expectedType, actualType);
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
            throw new InvalidFilterNodeException(
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

        throw new InvalidFilterNodeException(
            "Filter node must specify either Condition or Group.");
    }

    private static void ValidateFilterGroup(
        QueryFilterGroup group,
        RecordMetadata metadata)
    {
        if (group.Filters.Count == 0)
        {
            throw new EmptyFilterGroupException();
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
            throw new MissingFilterFieldException();
        }

        var field =
            GetField(
                metadata,
                condition.Field);

        if (!field.IsFilterable)
        {
            throw new FieldNotFilterableException(condition.Field);
        }

        if (!field.FilterOperators.Contains(condition.Operator))
        {
            throw new UnsupportedOperatorException(condition.Field, condition.Operator);
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
            throw new InvalidSearchNodeException(
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

        throw new InvalidSearchNodeException(
            "Search node must specify either Condition or Group.");
    }

    private static void ValidateSearchGroup(
        QuerySearchGroup group,
        RecordMetadata metadata)
    {
        if (group.Searches.Count == 0)
        {
            throw new EmptySearchGroupException();
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
            throw new MissingSearchTextException();
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
            throw new FieldNotSearchableException(
                $"No searchable fields exist for search field '{condition.Field ?? "*"}'.");
        }

        if (!list.Any(x =>
                x.MatchModes.Contains(
                    condition.MatchMode)))
        {
            throw new UnsupportedMatchModeException(condition.Field!, condition.MatchMode);
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
            throw new DuplicateSortFieldException(duplicateFields);
        }

        foreach (var sort in sorts)
        {
            var field =
                GetField(
                    metadata,
                    sort.Field);

            if (!field.IsSortable)
            {
                throw new FieldNotSortableException(sort.Field);
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
            ?? throw new PagingNotSupportedException(metadata.Name);

        if (page.Size is <= 0)
        {
            throw new InvalidPageSizeException(page.Size.Value, pageable.MaxSize);
        }

        if (page.Size.HasValue &&
            page.Size.Value > pageable.MaxSize)
        {
            throw new InvalidPageSizeException(page.Size.Value, pageable.MaxSize);
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
               ?? throw new InvalidFieldException(name, metadata.Name);
    }
}
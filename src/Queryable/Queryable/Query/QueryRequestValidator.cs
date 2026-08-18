using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

internal sealed class QueryRequestValidator : IQueryContextValidator
{
    public void Validate(
        IQueryRequest request,
        QueryContextRegistration registration,
        QueryViewRegistration viewRegistration)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(viewRegistration);

        ValidateInternal(
            request,
            registration.Metadata,
            viewRegistration.Metadata.Pageable);
    }

    public void Validate(
        IQueryRequest request,
        QueryContextRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registration);

        ValidateInternal(
            request,
            registration.Metadata,
            registration.Metadata.Pageable);
    }

    private static void ValidateInternal(
        IQueryRequest request,
        QueryContextMetadata metadata,
        PageableMetadata? pageable)
    {
        ValidateFilter(
            request.Query?.Filter,
            metadata);

        ValidateSearch(
            request.Query?.SearchText,
            metadata);

        ValidateSort(
            request.Query?.Sort,
            metadata);

        ValidatePage(
            request.Query?.Page,
            pageable);
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
        QueryContextMetadata metadata)
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
        QueryContextMetadata metadata)
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
        QueryContextMetadata metadata)
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
        string? searchText,
        QueryContextMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(
                searchText))
        {
            return;
        }

        if (!metadata.Fields.Any(
                x => x.IsSearchable))
        {
            throw new FieldNotSearchableException(
                "No searchable fields are defined.");
        }
    }

    private static void ValidateSort(
        IReadOnlyList<QuerySort>? sorts,
        QueryContextMetadata metadata)
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
        PageableMetadata? pageable)
    {
        if (page is null)
        {
            return;
        }

        if (pageable is null)
        {
            return;
        }

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
        QueryContextMetadata metadata,
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

using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

/// <summary>
/// Validates incoming QueryRequest instances against
/// record metadata.
///
/// Validation occurs before query compilation and execution.
///
/// Responsibilities:
///   - Field existence validation
///   - Operator support validation
///   - Search mode validation
///   - Sort validation
///   - Named query parameter validation
///   - Paging validation
///
/// This component must not execute queries or perform
/// provider-specific logic.
/// </summary>
internal interface IQueryContextValidator
{
    void Validate(IQueryRequest request, QueryContextRegistration registration, QueryViewRegistration viewRegistration);

    void Validate(IQueryRequest request, QueryContextRegistration registration);
}

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

        throw new KaleidoValidationException(
            ValidationErrorCodes.QryUnsupportedRuntimeType,
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

        throw new KaleidoValidationException(
            ValidationErrorCodes.QryInvalidParameterType,
            $"Parameter '{parameter.Name}' expects values of type '{expectedType.Name}' but received '{actualType.Name}'.");
    }

    private const int MaxFilterDepth = 10;

    private static void ValidateFilter(
        QueryFilterNode? node,
        QueryContextMetadata metadata,
        int depth = 0)
    {
        if (node is null)
        {
            return;
        }

        if (depth > MaxFilterDepth)
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryFilterDepthExceeded,
                $"Filter expression exceeds the maximum nesting depth of {MaxFilterDepth}.");
        }

        if (node.Condition is not null &&
            node.Group is not null)
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryInvalidFilterNode,
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
                metadata,
                depth);

            return;
        }

        throw new KaleidoValidationException(
            ValidationErrorCodes.QryInvalidFilterNode,
            "Filter node must specify either Condition or Group.");
    }

    private static void ValidateFilterGroup(
        QueryFilterGroup group,
        QueryContextMetadata metadata,
        int depth)
    {
        if (group.Filters.Count == 0)
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryEmptyFilterGroup,
                "Filter group must contain at least one expression.");
        }

        foreach (var child in group.Filters)
        {
            ValidateFilter(
                child,
                metadata,
                depth + 1);
        }
    }

    private static void ValidateFilterCondition(
        QueryFilterCondition condition,
        QueryContextMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(condition.Field))
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryMissingFilterField,
                "Filter field is required.");
        }

        var field =
            GetField(
                metadata,
                condition.Field);

        if (!field.IsFilterable)
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryFieldNotFilterable,
                $"Field '{condition.Field}' is not filterable.");
        }

        if (!field.FilterOperators.Contains(condition.Operator))
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryUnsupportedOperator,
                $"Field '{condition.Field}' does not support operator '{condition.Operator}'.");
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
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryFieldNotSearchable,
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
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryDuplicateSortField,
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
                throw new KaleidoValidationException(
                    ValidationErrorCodes.QryFieldNotSortable,
                    $"Field '{sort.Field}' is not sortable.");
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
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryInvalidPageSize,
                $"Page size '{page.Size.Value}' exceeds maximum page size '{pageable.MaxSize}'.");
        }

        if (page.Size.HasValue &&
            page.Size.Value > pageable.MaxSize)
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryInvalidPageSize,
                $"Page size '{page.Size.Value}' exceeds maximum page size '{pageable.MaxSize}'.");
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
               ?? throw new KaleidoValidationException(
                   ValidationErrorCodes.QryInvalidField,
                   $"Field '{name}' does not exist on record '{metadata.Name}'.");
    }
}

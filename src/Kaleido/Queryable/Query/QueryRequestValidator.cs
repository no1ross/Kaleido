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

internal sealed class QueryRequestValidator(ITypeDescriber typeDescriber) : IQueryContextValidator
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

    private void ValidateInternal(
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

    private void ValidateFilterValueTypes(QueryFilterCondition condition)
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

    private void ValidateSupportedRuntimeType(
        string name,
        object value)
    {
        var actualType =
            Nullable.GetUnderlyingType(
                value.GetType())
            ?? value.GetType();

        if (typeDescriber.IsSupportedType(actualType))
        {
            return;
        }

        throw new KaleidoValidationException(
            QueryableErrorCodes.UnsupportedRuntimeType,
            $"Value '{name}' contains unsupported runtime type '{actualType.FullName}'. " +
            "Transport layers must normalize values before invoking Queryable.");
    }

    private const int MaxFilterDepth = 10;

    private void ValidateFilter(
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
                QueryableErrorCodes.FilterDepthExceeded,
                $"Filter expression exceeds the maximum nesting depth of {MaxFilterDepth}.");
        }

        if (node.Condition is not null &&
            node.Group is not null)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidFilterNode,
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
            QueryableErrorCodes.InvalidFilterNode,
            "Filter node must specify either Condition or Group.");
    }

    private void ValidateFilterGroup(
        QueryFilterGroup group,
        QueryContextMetadata metadata,
        int depth)
    {
        if (group.Filters.Count == 0)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.EmptyFilterGroup,
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

    private void ValidateFilterCondition(
        QueryFilterCondition condition,
        QueryContextMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(condition.Field))
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.MissingFilterField,
                "Filter field is required.");
        }

        var field =
            metadata.GetField(
                condition.Field);

        if (!field.IsFilterable)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.FieldNotFilterable,
                $"Field '{condition.Field}' is not filterable.");
        }

        if (!field.FilterOperators.Contains(condition.Operator))
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.UnsupportedOperator,
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
                QueryableErrorCodes.FieldNotSearchable,
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
                QueryableErrorCodes.DuplicateSortField,
                $"Duplicate sort fields are not allowed: {string.Join(", ", duplicateFields)}.");
        }

        foreach (var sort in sorts)
        {
            var field =
                metadata.GetField(
                    sort.Field);

            if (!field.IsSortable)
            {
                throw new KaleidoValidationException(
                    QueryableErrorCodes.FieldNotSortable,
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
            throw new KaleidoValidationException(
                QueryableErrorCodes.PagingNotSupported,
                "Paging is not supported for this record.");
        }

        if (page.Size is <= 0)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidPageSize,
                $"Page size '{page.Size.Value}' must be greater than zero.");
        }

        if (page.Size.HasValue &&
            page.Size.Value > pageable.MaxSize)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidPageSize,
                $"Page size '{page.Size.Value}' exceeds maximum page size '{pageable.MaxSize}'.");
        }

        if (page.Offset is < 0)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidPageOffset,
                $"Page offset '{page.Offset.Value}' must not be negative.");
        }
    }

}

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

internal sealed class QueryRequestValidator(
    IDataTypeMapper dataTypeMapper)
    : IQueryContextValidator
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
        var fields = new FieldLookup(metadata);

        ValidateFilter(
            request.Query?.Filter,
            fields);

        ValidateSearch(
            request.Query?.SearchText,
            metadata);

        ValidateSort(
            request.Query?.Sort,
            fields);

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

        if (dataTypeMapper.IsSupportedType(actualType))
        {
            return;
        }

        throw new KaleidoValidationException(
            ValidationErrorCodes.QryUnsupportedRuntimeType,
            $"Value '{name}' contains unsupported runtime type '{actualType.FullName}'. " +
            "Transport layers must normalize values before invoking Queryable.");
    }

    private const int MaxFilterDepth = 10;

    private void ValidateFilter(
        QueryFilterNode? node,
        FieldLookup fields,
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
                fields);

            return;
        }

        if (node.Group is not null)
        {
            ValidateFilterGroup(
                node.Group,
                fields,
                depth);

            return;
        }

        throw new KaleidoValidationException(
            ValidationErrorCodes.QryInvalidFilterNode,
            "Filter node must specify either Condition or Group.");
    }

    private void ValidateFilterGroup(
        QueryFilterGroup group,
        FieldLookup fields,
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
                fields,
                depth + 1);
        }
    }

    private void ValidateFilterCondition(
        QueryFilterCondition condition,
        FieldLookup fields)
    {
        if (string.IsNullOrWhiteSpace(condition.Field))
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryMissingFilterField,
                "Filter field is required.");
        }

        var field =
            fields.Get(
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
        FieldLookup fields)
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
                fields.Get(
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

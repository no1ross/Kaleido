using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

/// <summary>
/// <para>
/// Validates incoming QueryRequest instances against
/// record metadata.
/// </para>
/// <para>Validation occurs before query compilation and execution.</para>
/// <para>
/// Responsibilities:
///   - Field existence validation
///   - Operator support validation
///   - Search mode validation
///   - Sort validation
///   - Query parameter validation (the parameters record's DataAnnotations; a missing record is
///     rejected when it has a required member)
///   - Paging validation
/// </para>
/// <para>
/// This component must not execute queries or perform
/// provider-specific logic.
/// </para>
/// </summary>
internal interface IQueryContextValidator
{
    /// <summary>
    /// Validates a request against a source's query context fields and the paging that applies
    /// (the view's for a view query, the source's for a direct or delegated query).
    /// </summary>
    void Validate(IQueryRequest request, QuerySourceMetadata metadata, PageableMetadata? pageable);
}

internal sealed class QueryRequestValidator(ITypeDescriber typeDescriber) : IQueryContextValidator
{
    public void Validate(
        IQueryRequest request,
        QuerySourceMetadata metadata,
        PageableMetadata? pageable)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metadata);

        ValidateParameters(request);

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

    // The parameters record is validated like a step's input: [Required] first, then the other
    // DataAnnotations. A missing record fails only when a member is required.
    private static void ValidateParameters(IQueryRequest request)
    {
        if (request.ViewParametersType == typeof(EmptyQueryViewParameters))
        {
            return;
        }

        var properties = TypeDescriptor.GetProperties(request.ViewParametersType).Cast<PropertyDescriptor>().ToArray();

        if (request.ViewParameters is not { } parameters)
        {
            if (properties.FirstOrDefault(x => x.Attributes.OfType<RequiredAttribute>().Any()) is { } required)
            {
                throw new KaleidoValidationException(
                    QueryableErrorCodes.MissingParameter,
                    $"Parameter '{required.Name}' is required.");
            }

            return;
        }

        foreach (var property in properties)
        {
            var attributes = property.Attributes.OfType<ValidationAttribute>().ToArray();

            if (attributes.Length == 0)
            {
                continue;
            }

            var context = new ValidationContext(parameters) { MemberName = property.Name, DisplayName = property.Name };
            var value = property.GetValue(parameters);

            var requiredAttribute = attributes.OfType<RequiredAttribute>().FirstOrDefault();

            if (requiredAttribute?.GetValidationResult(value, context) is { } missing)
            {
                // The attribute's own message only when the implementer wrote one.
                throw new KaleidoValidationException(
                    QueryableErrorCodes.MissingParameter,
                    requiredAttribute.ErrorMessage is null
                        ? $"Parameter '{property.Name}' is required."
                        : missing.ErrorMessage ?? $"Parameter '{property.Name}' is required.");
            }

            foreach (var attribute in attributes.Where(x => x is not RequiredAttribute))
            {
                if (attribute.GetValidationResult(value, context) is { } failure)
                {
                    throw new KaleidoValidationException(
                        QueryableErrorCodes.InvalidParameterValue,
                        failure.ErrorMessage ?? $"Parameter '{property.Name}' is not valid.");
                }
            }
        }
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
            $"A value for field '{name}' has unsupported runtime type '{actualType.FullName}'. " +
            "Transport layers must normalize values before invoking Queryable.");
    }

    private const int MaxFilterDepth = 10;

    private void ValidateFilter(
        QueryFilterNode? node,
        QuerySourceMetadata metadata,
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
        QuerySourceMetadata metadata,
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
        QuerySourceMetadata metadata)
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
        QuerySourceMetadata metadata)
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
        QuerySourceMetadata metadata)
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
                .Where(x => x.Skip(1).Any())
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
                "Paging is not supported for this query.");
        }

        if (page.Size is <= 0)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidPageSize,
                string.Create(CultureInfo.InvariantCulture, $"Page size '{page.Size.Value}' must be greater than zero."));
        }

        if (page.Size > pageable.MaxSize)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidPageSize,
                string.Create(CultureInfo.InvariantCulture, $"Page size '{page.Size.Value}' exceeds maximum page size '{pageable.MaxSize}'."));
        }

        if (page.Offset is < 0)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidPageOffset,
                string.Create(CultureInfo.InvariantCulture, $"Page offset '{page.Offset.Value}' must not be negative."));
        }
    }

}

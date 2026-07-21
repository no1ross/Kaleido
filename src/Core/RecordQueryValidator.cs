using Kaleido.Metadata;
using Kaleido.Validation;

namespace Kaleido;

public sealed class RecordQueryValidator : IRecordQueryValidator
{
    public void Validate(
        KaleidoQueryRequest request,
        RuntimeRecordMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metadata);

        ValidateNamedQuery(request, metadata);
        ValidateFilter(request.Query?.Filter, metadata);
        ValidateSearch(request.Query?.Search, metadata);
        ValidateSort(request.Query?.Sort, metadata);
        ValidatePage(request.Query?.Page, metadata);
    }

    private static void ValidateNamedQuery(
        KaleidoQueryRequest request,
        RuntimeRecordMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(request.QueryName))
        {
            return;
        }

        var allowed = metadata.AllowedQueries.SingleOrDefault(x =>
            string.Equals(
                x.Name,
                request.QueryName,
                StringComparison.OrdinalIgnoreCase));

        if (allowed is null)
        {
            throw new InvalidOperationException(
                $"Named query '{request.QueryName}' is not allowed for record '{metadata.Name}'.");
        }

        foreach (var parameter in allowed.Parameters)
        {
            if (request.Parameters is null ||
                !request.Parameters.ContainsKey(parameter) ||
                request.Parameters[parameter] is null)
            {
                throw new InvalidOperationException(
                    $"Named query '{request.QueryName}' requires parameter '{parameter}'.");
            }
        }
    }

    private static void ValidateFilter(
        QueryFilterNode? node,
        RuntimeRecordMetadata metadata)
    {
        if (node is null)
        {
            return;
        }

        if (node.Condition is not null && node.Group is not null)
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
        RuntimeRecordMetadata metadata)
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
        RuntimeRecordMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(condition.Field))
        {
            throw new InvalidOperationException(
                "Filter field is required.");
        }

        var field = GetField(
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
    }

    private static void ValidateSearch(
        QuerySearchNode? node,
        RuntimeRecordMetadata metadata)
    {
        if (node is null)
        {
            return;
        }

        if (node.Condition is not null && node.Group is not null)
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
        RuntimeRecordMetadata metadata)
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
        RuntimeRecordMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(condition.SearchText))
        {
            throw new InvalidOperationException(
                "Search text is required.");
        }

        var fields = metadata.Fields
            .Where(x => x.IsSearchable);

        if (!string.IsNullOrWhiteSpace(condition.Field))
        {
            fields = fields.Where(x =>
                string.Equals(
                    x.Name,
                    condition.Field,
                    StringComparison.OrdinalIgnoreCase));
        }

        var list = fields.ToArray();

        if (list.Length == 0)
        {
            throw new InvalidOperationException(
                $"No searchable fields exist for search field '{condition.Field ?? "*"}'.");
        }

        if (!list.Any(x => x.MatchModes.Contains(condition.MatchMode)))
        {
            throw new InvalidOperationException(
                $"Match mode '{condition.MatchMode}' is not supported for search field '{condition.Field ?? "*"}'.");
        }
    }

    private static void ValidateSort(
        IReadOnlyList<QuerySort>? sorts,
        RuntimeRecordMetadata metadata)
    {
        if (sorts is null)
        {
            return;
        }

        foreach (var sort in sorts)
        {
            var field = GetField(
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
        RuntimeRecordMetadata metadata)
    {
        if (page is null)
        {
            return;
        }

        var pageable = metadata.Pageable
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

    private static RuntimeFieldMetadata GetField(
        RuntimeRecordMetadata metadata,
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
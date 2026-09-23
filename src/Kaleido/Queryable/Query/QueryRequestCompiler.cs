using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

/// <summary>
/// Converts a validated QueryRequest into an optimized
/// provider-neutral CompiledRecordQuery.
///
/// Compilation resolves field references, operators,
/// search modes, paging definitions, and named query
/// metadata into runtime structures suitable for execution.
///
/// The resulting compiled query may be reused across
/// multiple providers.
///
/// This interface exists to separate validation from execution.
/// </summary>
internal interface IQueryContextCompiler
{
    CompiledRecordQuery Compile(IQueryRequest request, QueryContextMetadata metadata, QueryViewMetadata queryViewMetadata);

    CompiledRecordQuery Compile(IQueryRequest request, QueryContextMetadata metadata);
}

internal sealed class QueryRequestCompiler : IQueryContextCompiler
{
    public CompiledRecordQuery Compile(
        IQueryRequest request,
        QueryContextMetadata metadata,
        QueryViewMetadata queryViewMetadata)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(queryViewMetadata);

        return CompileInternal(
            request,
            metadata,
            queryViewMetadata.Pageable);
    }

    public CompiledRecordQuery Compile(
        IQueryRequest request,
        QueryContextMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metadata);

        return CompileInternal(
            request,
            metadata,
            metadata.Pageable);
    }

    private static CompiledRecordQuery CompileInternal(
        IQueryRequest request,
        QueryContextMetadata metadata,
        PageableMetadata? pageable)
    {
        var size = request.Query?.Page?.Size
                   ?? pageable?.DefaultSize
                   ?? 50;

        var maxSize = pageable?.MaxSize ?? int.MaxValue;

        size = Math.Min(size, maxSize);

        var offset = request.Query?.Page?.Offset ?? 0;

        var fields = new FieldLookup(metadata);

        return new CompiledRecordQuery(
            CompileFilter(request.Query?.Filter, fields),
            CompileSearch(request.Query?.SearchText, metadata),
            CompileSort(request.Query?.Sort, fields),
            new CompiledPage(size, offset));
    }

    private static CompiledFilterExpression? CompileFilter(
        QueryFilterNode? node,
        FieldLookup fields)
    {
        if (node is null)
        {
            return null;
        }

        if (node.Condition is not null && node.Group is not null)
        {
            throw new KaleidoValidationException(
                ValidationErrorCodes.QryInvalidFilterNode,
                "Filter node cannot specify both Condition and Group.");
        }

        if (node.Condition is not null)
        {
            return CompileFilterCondition(
                node.Condition,
                fields);
        }

        if (node.Group is not null)
        {
            return CompileFilterGroup(
                node.Group,
                fields);
        }

        throw new KaleidoValidationException(
            ValidationErrorCodes.QryInvalidFilterNode,
            "Filter node must specify either Condition or Group.");
    }

    private static CompiledFilterCondition CompileFilterCondition(
        QueryFilterCondition condition,
        FieldLookup fields)
    {
        return new CompiledFilterCondition(
            fields.Get(condition.Field),
            condition.Operator,
            condition.Values);
    }

    private static CompiledFilterGroup CompileFilterGroup(
        QueryFilterGroup group,
        FieldLookup fields)
    {
        var compiledFilters = group.Filters
            .Select(x => CompileFilter(x, fields))
            .OfType<CompiledFilterExpression>()
            .ToArray();

        return new CompiledFilterGroup(
            group.Operator,
            compiledFilters);
    }

    private static CompiledSearch? CompileSearch(
        string? searchText,
        QueryContextMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        var searchableFields = metadata.Fields
            .Where(x => x.IsSearchable)
            .OrderBy(x => x.SearchPriority ?? int.MaxValue)
            .Select(x =>
            {
                if (x.MatchMode is null)
                {
                    throw new KaleidoFrameworkException(
                        FrameworkErrorCodes.TypeMismatch,
                        $"Field '{x.Name}' is marked as searchable but has no MatchMode configured.");
                }

                return new CompiledSearchField(
                    x,
                    x.MatchMode.Value,
                    x.SearchPriority ?? int.MaxValue);
            })
            .ToArray();

        return new CompiledSearch(
            searchText,
            searchableFields);
    }

    private static IReadOnlyList<CompiledSort> CompileSort(
        IReadOnlyList<QuerySort>? sorts,
        FieldLookup fields)
    {
        if (sorts is null || sorts.Count == 0)
        {
            return Array.Empty<CompiledSort>();
        }

        return sorts
            .OrderBy(x => x.Sequence ?? int.MaxValue)
            .Select((x, index) =>
                new CompiledSort(
                    fields.Get(x.Field),
                    x.Direction,
                    index))
            .ToArray();
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

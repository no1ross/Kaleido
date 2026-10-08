using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

/// <summary>
/// Converts a validated QueryRequest into an optimized
/// provider-neutral CompiledQuery.
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
    /// <summary>
    /// Compiles a validated request against a source's query context fields and the paging that
    /// applies (the view's for a view query, the source's for a direct query).
    /// </summary>
    CompiledQuery Compile(IQueryRequest request, QuerySourceMetadata metadata, PageableMetadata? pageable);
}

internal sealed class QueryRequestCompiler : IQueryContextCompiler
{
    public CompiledQuery Compile(
        IQueryRequest request,
        QuerySourceMetadata metadata,
        PageableMetadata? pageable)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metadata);

        var size = request.Query?.Page?.Size
                   ?? pageable?.DefaultSize
                   ?? 50;

        var maxSize = pageable?.MaxSize ?? int.MaxValue;

        size = Math.Min(size, maxSize);

        var offset = request.Query?.Page?.Offset ?? 0;

        return new CompiledQuery(
            CompileFilter(request.Query?.Filter, metadata),
            CompileSearch(request.Query?.SearchText, metadata),
            CompileSort(request.Query?.Sort, metadata),
            new CompiledPage(size, offset, IsExplicit: request.Query?.Page is not null));
    }

    private static CompiledFilterExpression? CompileFilter(
        QueryFilterNode? node,
        QuerySourceMetadata metadata)
    {
        if (node is null)
        {
            return null;
        }

        if (node.Condition is not null && node.Group is not null)
        {
            throw new KaleidoValidationException(
                QueryableErrorCodes.InvalidFilterNode,
                "Filter node cannot specify both Condition and Group.");
        }

        if (node.Condition is not null)
        {
            return CompileFilterCondition(
                node.Condition,
                metadata);
        }

        if (node.Group is not null)
        {
            return CompileFilterGroup(
                node.Group,
                metadata);
        }

        throw new KaleidoValidationException(
            QueryableErrorCodes.InvalidFilterNode,
            "Filter node must specify either Condition or Group.");
    }

    private static CompiledFilterCondition CompileFilterCondition(
        QueryFilterCondition condition,
        QuerySourceMetadata metadata)
    {
        return new CompiledFilterCondition(
            metadata.GetField(condition.Field),
            condition.Operator,
            condition.Values);
    }

    private static CompiledFilterGroup CompileFilterGroup(
        QueryFilterGroup group,
        QuerySourceMetadata metadata)
    {
        var compiledFilters = group.Filters
            .Select(x => CompileFilter(x, metadata))
            .OfType<CompiledFilterExpression>()
            .ToArray();

        return new CompiledFilterGroup(
            group.Operator,
            compiledFilters);
    }

    private static CompiledSearch? CompileSearch(
        string? searchText,
        QuerySourceMetadata metadata)
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
        QuerySourceMetadata metadata)
    {
        if (sorts is null || sorts.Count == 0)
        {
            return Array.Empty<CompiledSort>();
        }

        return sorts
            .OrderBy(x => x.Sequence ?? int.MaxValue)
            .Select((x, index) =>
                new CompiledSort(
                    metadata.GetField(x.Field),
                    x.Direction,
                    index))
            .ToArray();
    }
}

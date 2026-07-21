using Kaleido.Metadata;

namespace Kaleido;

public sealed class RecordQueryCompiler : IRecordQueryCompiler
{
    public CompiledRecordQuery Compile(
        KaleidoQueryRequest request,
        RuntimeRecordMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metadata);

        var pageable = metadata.Pageable;

        var size = request.Query?.Page?.Size
                   ?? pageable?.DefaultSize
                   ?? 50;

        var maxSize = pageable?.MaxSize ?? int.MaxValue;

        size = Math.Min(size, maxSize);

        var offset = request.Query?.Page?.Offset ?? 0;

        return new CompiledRecordQuery(
            request.QueryName,
            request.Parameters,
            CompileFilter(request.Query?.Filter, metadata),
            CompileSearch(request.Query?.Search, metadata),
            CompileSort(request.Query?.Sort, metadata),
            new CompiledPage(size, offset));
    }

    private static CompiledFilterExpression? CompileFilter(
        QueryFilterNode? node,
        RuntimeRecordMetadata metadata)
    {
        if (node is null)
        {
            return null;
        }

        if (node.Condition is not null && node.Group is not null)
        {
            throw new InvalidOperationException(
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

        throw new InvalidOperationException(
            "Filter node must specify either Condition or Group.");
    }

    private static CompiledFilterCondition CompileFilterCondition(
        QueryFilterCondition condition,
        RuntimeRecordMetadata metadata)
    {
        return new CompiledFilterCondition(
            GetField(metadata, condition.Field),
            condition.Operator,
            condition.Values);
    }

    private static CompiledFilterGroup CompileFilterGroup(
        QueryFilterGroup group,
        RuntimeRecordMetadata metadata)
    {
        return new CompiledFilterGroup(
            group.Operator,
            group.Filters
                .Select(x => CompileFilter(x, metadata)!)
                .ToArray());
    }

    private static CompiledSearchExpression? CompileSearch(
        QuerySearchNode? node,
        RuntimeRecordMetadata metadata)
    {
        if (node is null)
        {
            return null;
        }

        if (node.Condition is not null && node.Group is not null)
        {
            throw new InvalidOperationException(
                "Search node cannot specify both Condition and Group.");
        }

        if (node.Condition is not null)
        {
            return CompileSearchCondition(
                node.Condition,
                metadata);
        }

        if (node.Group is not null)
        {
            return CompileSearchGroup(
                node.Group,
                metadata);
        }

        throw new InvalidOperationException(
            "Search node must specify either Condition or Group.");
    }

    private static CompiledSearchExpression CompileSearchCondition(
        QuerySearchCondition condition,
        RuntimeRecordMetadata metadata)
    {
        var conditions = metadata.Fields
            .Where(x => x.IsSearchable)
            .Where(x => x.MatchModes.Contains(condition.MatchMode))
            .Where(x =>
                condition.Field is null ||
                string.Equals(
                    x.Name,
                    condition.Field,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.SearchPriority ?? int.MaxValue)
            .Select(x =>
                (CompiledSearchExpression)new CompiledSearchCondition(
                    x,
                    condition.SearchText,
                    condition.MatchMode))
            .ToArray();

        return conditions.Length == 1
            ? conditions[0]
            : new CompiledSearchGroup(
                LogicalOperator.Or,
                conditions);
    }

    private static CompiledSearchGroup CompileSearchGroup(
        QuerySearchGroup group,
        RuntimeRecordMetadata metadata)
    {
        return new CompiledSearchGroup(
            group.Operator,
            group.Searches
                .Select(x => CompileSearch(x, metadata)!)
                .ToArray());
    }

    private static IReadOnlyList<CompiledSort> CompileSort(
        IReadOnlyList<QuerySort>? sorts,
        RuntimeRecordMetadata metadata)
    {
        if (sorts is null || sorts.Count == 0)
        {
            return Array.Empty<CompiledSort>();
        }

        return sorts
            .OrderBy(x => x.Sequence ?? int.MaxValue)
            .Select((x, index) =>
                new CompiledSort(
                    GetField(metadata, x.Field),
                    x.Direction,
                    index))
            .ToArray();
    }

    private static RuntimeFieldMetadata GetField(
        RuntimeRecordMetadata metadata,
        string fieldName)
    {
        var field = metadata.Fields.SingleOrDefault(x =>
            string.Equals(
                x.Name,
                fieldName,
                StringComparison.OrdinalIgnoreCase));

        return field
            ?? throw new InvalidOperationException(
                $"Field '{fieldName}' is not defined for record '{metadata.Name}'.");
    }
}
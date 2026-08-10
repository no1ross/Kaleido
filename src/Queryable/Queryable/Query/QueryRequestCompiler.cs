using Kaleido.Queryable.Metadata;
using System.Xml.Linq;

namespace Kaleido.Queryable.Query;

internal sealed class QueryRequestCompiler : IRecordQueryCompiler
{
    public CompiledRecordQuery Compile(
        QueryRequest request,
        RecordMetadata metadata)
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
            CompiledNamedQuery(request.NamedQuery, metadata),
            CompileFilter(request.Query?.Filter, metadata),
            CompileSearch(request.Query?.SearchText, metadata),
            CompileSort(request.Query?.Sort, metadata),
            new CompiledPage(size, offset));
    }

    private static NamedQuery? CompiledNamedQuery(NamedQuery? namedQuery, RecordMetadata metadata)
    {
        if (namedQuery is null)
        {
            return null;
        }

        return new NamedQuery(
            namedQuery.Name,
            namedQuery.Parameters
        );
    }


    private static CompiledFilterExpression? CompileFilter(
        QueryFilterNode? node,
        RecordMetadata metadata)
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
        RecordMetadata metadata)
    {
        return new CompiledFilterCondition(
            GetField(metadata, condition.Field),
            condition.Operator,
            condition.Values);
    }

    private static CompiledFilterGroup CompileFilterGroup(
        QueryFilterGroup group,
        RecordMetadata metadata)
    {
        return new CompiledFilterGroup(
            group.Operator,
            group.Filters
                .Select(x => CompileFilter(x, metadata)!)
                .ToArray());
    }

    private static CompiledSearch? CompileSearch(
        string? searchText,
        RecordMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        return new CompiledSearch(
            searchText,
            metadata.Fields
                .Where(x => x.IsSearchable)
                .OrderBy(x => x.SearchPriority ?? int.MaxValue)
                .Select(x =>
                    new CompiledSearchField(
                        x,
                        x.MatchMode!.Value,
                        x.SearchPriority ?? int.MaxValue))
                .ToArray());
    }

    private static IReadOnlyList<CompiledSort> CompileSort(
        IReadOnlyList<QuerySort>? sorts,
        RecordMetadata metadata)
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

    private static FieldMetadata GetField(RecordMetadata metadata, string fieldName)
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

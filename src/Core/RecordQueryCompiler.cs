using Kaleido.Metadata;

namespace Kaleido;

public sealed class RecordQueryCompiler : IRecordQueryCompiler
{
    public CompiledRecordQuery Compile(KaleidoQueryRequest request, RuntimeRecordMetadata metadata)
    {
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

    private static CompiledFilterExpression? CompileFilter(IFilterExpression? expression, RuntimeRecordMetadata metadata)
    {
        return expression switch
        {
            null => null,
            QueryFilter f => new CompiledFilterCondition(GetField(metadata, f.Field), f.Operator, f.Values),
            QueryFilterGroup g => new CompiledFilterGroup(g.Operator, g.Expressions.Select(x => CompileFilter(x, metadata)!).ToArray()),
            _ => throw new NotSupportedException($"Unsupported filter expression type '{expression.GetType().Name}'.")
        };
    }

    private static CompiledSearchExpression? CompileSearch(ISearchExpression? expression, RuntimeRecordMetadata metadata)
    {
        return expression switch
        {
            null => null,
            QuerySearch s => CompileSearchCondition(s, metadata),
            QuerySearchGroup g => new CompiledSearchGroup(g.Operator, g.Expressions.Select(x => CompileSearch(x, metadata)!).ToArray()),
            _ => throw new NotSupportedException($"Unsupported search expression type '{expression.GetType().Name}'.")
        };
    }

    private static CompiledSearchExpression CompileSearchCondition(QuerySearch search, RuntimeRecordMetadata metadata)
    {
        var conditions = metadata.Fields
            .Where(x => x.IsSearchable)
            .Where(x => x.MatchModes.Contains(search.MatchMode))
            .Where(x => search.Field is null || string.Equals(x.Name, search.Field, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.SearchPriority ?? int.MaxValue)
            .Select(x => (CompiledSearchExpression)new CompiledSearchCondition(x, search.SearchText, search.MatchMode))
            .ToArray();
        return conditions.Length == 1 ? conditions[0] : new CompiledSearchGroup(LogicalOperator.Or, conditions);
    }

    private static IReadOnlyList<CompiledSort> CompileSort(IReadOnlyList<QuerySort>? sorts, RuntimeRecordMetadata metadata)
    {
        if (sorts is null || sorts.Count == 0) return Array.Empty<CompiledSort>();
        return sorts.OrderBy(x => x.Sequence ?? int.MaxValue).Select((x, index) => new CompiledSort(GetField(metadata, x.Field), x.Direction, index)).ToArray();
    }

    private static RuntimeFieldMetadata GetField(RuntimeRecordMetadata metadata, string fieldName)
    {
        var field = metadata.Fields.SingleOrDefault(x => string.Equals(x.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        return field ?? 
            throw new InvalidOperationException($"Field '{fieldName}' is not defined for record '{metadata.Name}'.");
    }
}

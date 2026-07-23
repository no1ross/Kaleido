using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable;

public sealed record CompiledRecordQuery(
    string? NamedQuery,
    IReadOnlyDictionary<string, object?>? Parameters,
    CompiledFilterExpression? Filter,
    CompiledSearchExpression? Search,
    IReadOnlyList<CompiledSort> Sort,
    CompiledPage Page);

public abstract record CompiledFilterExpression;
public sealed record CompiledFilterGroup(LogicalOperator Operator, IReadOnlyList<CompiledFilterExpression> Expressions) : CompiledFilterExpression;
public sealed record CompiledFilterCondition(RuntimeFieldMetadata Field, FilterOperator Operator, IReadOnlyList<object?> Values) : CompiledFilterExpression;

public abstract record CompiledSearchExpression;
public sealed record CompiledSearchGroup(LogicalOperator Operator, IReadOnlyList<CompiledSearchExpression> Expressions) : CompiledSearchExpression;
public sealed record CompiledSearchCondition(RuntimeFieldMetadata Field, string SearchText, MatchMode MatchMode) : CompiledSearchExpression;

public sealed record CompiledSort(RuntimeFieldMetadata Field, SortDirection Direction, int Sequence);
public sealed record CompiledPage(int Size, int Offset);

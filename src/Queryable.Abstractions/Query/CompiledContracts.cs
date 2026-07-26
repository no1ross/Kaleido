using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

public sealed record CompiledRecordQuery(
    KaleidoNamedQuery? NamedQuery,
    CompiledFilterExpression? Filter,
    CompiledSearchExpression? Search,
    IReadOnlyList<CompiledSort> Sort,
    CompiledPage Page);

public abstract record CompiledFilterExpression;
public sealed record CompiledFilterGroup(LogicalOperator Operator, IReadOnlyList<CompiledFilterExpression> Filters) : CompiledFilterExpression;
public sealed record CompiledFilterCondition(FieldMetadata Field, FilterOperator Operator, IReadOnlyList<object?> Values) : CompiledFilterExpression;

public abstract record CompiledSearchExpression;
public sealed record CompiledSearchGroup(LogicalOperator Operator, IReadOnlyList<CompiledSearchExpression> Searches) : CompiledSearchExpression;
public sealed record CompiledSearchCondition(FieldMetadata Field, string SearchText, MatchMode MatchMode) : CompiledSearchExpression;

public sealed record CompiledSort(FieldMetadata Field, SortDirection Direction, int Sequence);
public sealed record CompiledPage(int Size, int Offset);

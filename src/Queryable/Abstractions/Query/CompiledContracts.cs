using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

public sealed record CompiledRecordQuery(
    NamedQuery? NamedQuery,
    CompiledFilterExpression? Filter,
    CompiledSearch? Search,
    IReadOnlyList<CompiledSort> Sort,
    CompiledPage Page);

public abstract record CompiledFilterExpression;
public sealed record CompiledFilterGroup(LogicalOperator Operator, IReadOnlyList<CompiledFilterExpression> Filters) : CompiledFilterExpression;
public sealed record CompiledFilterCondition(FieldMetadata Field, FilterOperator Operator, IReadOnlyList<object?> Values) : CompiledFilterExpression;

public sealed record CompiledSearch
(
    string SearchText,
    IReadOnlyList<CompiledSearchField> Fields
);
public sealed record CompiledSearchField
(
    FieldMetadata Field,
    MatchMode MatchMode,
    int Priority
);

public sealed record CompiledSort(FieldMetadata Field, SortDirection Direction, int Sequence);
public sealed record CompiledPage(int Size, int Offset);

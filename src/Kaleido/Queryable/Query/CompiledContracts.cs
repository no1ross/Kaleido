using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

internal sealed record CompiledRecordQuery(
    CompiledFilterExpression? Filter,
    CompiledSearch? Search,
    IReadOnlyList<CompiledSort> Sort,
    CompiledPage Page);

internal abstract record CompiledFilterExpression;
internal sealed record CompiledFilterGroup(LogicalOperator Operator, IReadOnlyList<CompiledFilterExpression> Filters) : CompiledFilterExpression;
internal sealed record CompiledFilterCondition(FieldMetadata Field, FilterOperator Operator, IReadOnlyList<object?> Values) : CompiledFilterExpression;

internal sealed record CompiledSearch
(
    string SearchText,
    IReadOnlyList<CompiledSearchField> Fields
);
internal sealed record CompiledSearchField
(
    FieldMetadata Field,
    MatchMode MatchMode,
    int Priority
);

internal sealed record CompiledSort(FieldMetadata Field, SortDirection Direction, int Sequence);
internal sealed record CompiledPage(int Size, int Offset);

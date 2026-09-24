using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

[ExcludeFromCodeCoverage]
internal sealed record CompiledRecordQuery(
    CompiledFilterExpression? Filter,
    CompiledSearch? Search,
    IReadOnlyList<CompiledSort> Sort,
    CompiledPage Page);

[ExcludeFromCodeCoverage]
internal abstract record CompiledFilterExpression;
[ExcludeFromCodeCoverage]
internal sealed record CompiledFilterGroup(LogicalOperator Operator, IReadOnlyList<CompiledFilterExpression> Filters) : CompiledFilterExpression;
[ExcludeFromCodeCoverage]
internal sealed record CompiledFilterCondition(FieldMetadata Field, FilterOperator Operator, IReadOnlyList<object?> Values) : CompiledFilterExpression;

[ExcludeFromCodeCoverage]
internal sealed record CompiledSearch
(
    string SearchText,
    IReadOnlyList<CompiledSearchField> Fields
);
[ExcludeFromCodeCoverage]
internal sealed record CompiledSearchField
(
    FieldMetadata Field,
    MatchMode MatchMode,
    int Priority
);

[ExcludeFromCodeCoverage]
internal sealed record CompiledSort(FieldMetadata Field, SortDirection Direction, int Sequence);
[ExcludeFromCodeCoverage]
internal sealed record CompiledPage(int Size, int Offset);

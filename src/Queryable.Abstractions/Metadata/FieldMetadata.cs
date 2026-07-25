namespace Kaleido.Queryable.Metadata;

public sealed record FieldMetadata
(
    string Name,
    Type FieldType,
    bool IsFilterable,
    IReadOnlyList<FilterOperator> FilterOperators,
    bool IsSearchable,
    int? SearchPriority,
    IReadOnlyList<MatchMode> MatchModes,
    bool IsSortable
)
{
    public bool IsNullable => Nullable.GetUnderlyingType(FieldType) != null;
};

using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableFieldMetadata
{
    public required string Name { get; init; }

    public required DataTypeDescriptor DataType { get; init; }

    public bool IsFilterable { get; init; }

    public IReadOnlyCollection<FilterOperator> FilterOperators { get; init; }
        = Array.Empty<FilterOperator>();

    public bool IsSearchable { get; init; }

    public int? SearchPriority { get; init; }

    public MatchMode? MatchMode { get; init; }

    public bool IsSortable { get; init; }

    public static QueryableFieldMetadata FromMetadata(
        FieldMetadata metadata)
    {
        return new QueryableFieldMetadata
        {
            Name = metadata.Name,
            DataType = DataTypeMapper.GetDescriptor(metadata.FieldType),
            IsFilterable = metadata.IsFilterable,
            FilterOperators = metadata.FilterOperators,
            IsSearchable = metadata.IsSearchable,
            SearchPriority = metadata.SearchPriority,
            MatchMode = metadata.MatchMode,
            IsSortable = metadata.IsSortable
        };
    }
}

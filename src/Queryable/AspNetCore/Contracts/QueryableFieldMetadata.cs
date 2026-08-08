using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableFieldMetadata
{
    public required string Name { get; init; }

    public required DataTypeDescriptor DataType { get; init; }

    public bool IsFilterable { get; init; }

    public IReadOnlyCollection<string> FilterOperators { get; init; }
        = Array.Empty<string>();

    public bool IsSearchable { get; init; }

    public int? SearchPriority { get; init; }

    public IReadOnlyCollection<string> MatchModes { get; init; }
        = Array.Empty<string>();

    public bool IsSortable { get; init; }

    public static QueryableFieldMetadata FromMetadata(
        FieldMetadata metadata)
    {
        return new QueryableFieldMetadata
        {
            Name = metadata.Name,
            DataType = DataTypeMapper.GetDescriptor(metadata.FieldType),
            IsFilterable = metadata.IsFilterable,
            FilterOperators = metadata.FilterOperators
                .Select(x => x.ToString())
                .ToArray(),
            IsSearchable = metadata.IsSearchable,
            SearchPriority = metadata.SearchPriority,
            MatchModes = metadata.MatchModes
                .Select(x => x.ToString())
                .ToArray(),
            IsSortable = metadata.IsSortable
        };
    }
}

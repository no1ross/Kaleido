using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableQueryParameter
{
    public required string Name { get; init; }

    public required DataTypeDescriptor DataType { get; init; }

    public bool Required { get; init; }

    public string? Description { get; init; }

    public object? DefaultValue { get; init; }

    public static QueryableQueryParameter FromMetadata(QueryParameterMetadata metadata)
    {
        return new QueryableQueryParameter
        {
            Name = metadata.Name,
            DataType = DataTypeMapper.GetDescriptor(metadata.Type),
            Required = metadata.Required,
            Description = metadata.Description,
            DefaultValue = metadata.DefaultValue
        };
    }
}

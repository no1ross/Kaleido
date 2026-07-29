using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryParameterContract
{
    public required string Name { get; init; }

    public required DataTypeDescriptor DataType { get; init; }

    public bool Required { get; init; }

    public string? Description { get; init; }

    public object? DefaultValue { get; init; }

    public static QueryParameterContract FromMetadata(QueryParameterMetadata metadata)
    {
        return new QueryParameterContract
        {
            Name = metadata.Name,
            DataType = DataTypeMapper.GetDescriptor(metadata.Type),
            Required = metadata.Required,
            Description = metadata.Description,
            DefaultValue = metadata.DefaultValue
        };
    }
}

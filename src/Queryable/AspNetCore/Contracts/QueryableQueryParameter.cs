using Kaleido.Queryable.Metadata;
using System.Reflection;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableQueryParameter
{
    public required string Name { get; init; }

    public required DataTypeDescriptor DataType { get; init; }


    public IReadOnlyCollection<ConstraintContract> Constraints
    {
        get;
        init;
    }
        = [];
    
    public static QueryableQueryParameter FromMetadata(QueryParameterMetadata metadata)
    {
        return new QueryableQueryParameter
        {
            Name = metadata.Name,
            DataType = DataTypeMapper.GetDescriptor(metadata.Type),
            Constraints = metadata.Constraints
        };
    }
}

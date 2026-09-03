using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableQueryParameter : QueryableParameterDescriptor
{
    public static QueryableQueryParameter FromRegistryItem(
        QueryableParameterDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new QueryableQueryParameter
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType,
            Constraints = item.Constraints
        };
    }
}

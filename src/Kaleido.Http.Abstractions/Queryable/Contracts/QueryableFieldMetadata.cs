using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableFieldMetadata : QueryableFieldDescriptor
{
    public static QueryableFieldMetadata FromRegistryItem(
        QueryableFieldDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new QueryableFieldMetadata
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType,
            IsFilterable = item.IsFilterable,
            FilterOperators = item.FilterOperators,
            IsSearchable = item.IsSearchable,
            SearchPriority = item.SearchPriority,
            MatchMode = item.MatchMode,
            IsSortable = item.IsSortable
        };
    }
}

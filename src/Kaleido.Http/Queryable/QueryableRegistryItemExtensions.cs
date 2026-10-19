using Kaleido.Http.Registry;

namespace Kaleido.Http.Queryable;

// Projects a registered query source, with its fields, parameters, output fields and views, to its
// wire shape, building its registry and query URLs (route segments use the lowercase names). The
// wire contracts own their own types, so the core registry items stay internal (#356).
internal static class QueryableRegistryItemExtensions
{
    public static QueryableSourceResponse ToResponse(
        this QueryableSourceRegistryItem item,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(item);

        var sourceName = item.Name.ToLowerInvariant();

        return new QueryableSourceResponse
        {
            ServiceName = serviceName,
            Name = item.Name,
            Description = item.Description,
            DisplayName = item.DisplayName,
            Version = item.Version,
            Source = item.Source,
            Pageable = item.Pageable.ToWire(),
            Authorization = item.Authorization,
            RegistryUrl = RegistryContractUrls.Registry(serviceName),
            QueryUrl = QueryableContractUrls.QuerySourceQuery(serviceName, sourceName),
            Fields = [.. item.Fields.Select(f => f.ToWire())],
            Parameters = [.. item.Parameters.Select(p => p.ToWire())],
            OutputFields = [.. item.OutputFields.Select(o => o.ToWire())],
            Views = [.. item.Views.Select(view => view.ToResponse(sourceName, serviceName))],
        };
    }

    public static QueryableViewResponse ToResponse(
        this QueryableViewRegistryItem item,
        string sourceName,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        return new QueryableViewResponse
        {
            Name = item.Name,
            Description = item.Description,
            DisplayName = item.DisplayName,
            Version = item.Version,
            Pageable = item.Pageable.ToWire(),
            Authorization = item.Authorization,
            QueryUrl = QueryableContractUrls.QueryViewQuery(serviceName, sourceName, item.Name.ToLowerInvariant()),
            Parameters = [.. item.Parameters.Select(p => p.ToWire())],
            OutputFields = [.. item.OutputFields.Select(o => o.ToWire())],
        };
    }

    public static QueryableFieldMetadata ToWire(this QueryableFieldDescriptor item) =>
        new()
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType.ToWire(),
            Classifications = item.Classifications,
            IsFilterable = item.IsFilterable,
            FilterOperators = item.FilterOperators,
            IsSearchable = item.IsSearchable,
            SearchPriority = item.SearchPriority,
            MatchMode = item.MatchMode,
            IsSortable = item.IsSortable,
        };

    public static QueryableQueryParameter ToWire(this QueryableParameterDescriptor item) =>
        new()
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType.ToWire(),
            Classifications = item.Classifications,
            Constraints = item.Constraints.ToWire(),
        };

    public static QueryableQueryProperty ToWire(this QueryableOutputFieldDescriptor item) =>
        new()
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType.ToWire(),
            Classifications = item.Classifications,
        };
}

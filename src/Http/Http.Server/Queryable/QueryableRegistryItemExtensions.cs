using Kaleido.Http.Server.Registry;

namespace Kaleido.Http.Server.Queryable;

// Projects a registered query source, with its fields, parameters, output fields and views, to its
// wire shape, building its registry and query URLs (route segments use the lowercase names). The
// wire contracts own their own types, so the core registry items stay internal (#356).
internal static class QueryableRegistryItemExtensions
{
    public static QueryableSourceResponse ToResponse(
        this QueryableSourceRegistryItem item,
        KaleidoServiceOptions serviceOptions)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(serviceOptions);

        var serviceName = serviceOptions.ServiceName;
        var version = serviceOptions.ServiceVersion ?? throw new KaleidoConfigurationException(
            ConfigurationErrorCodes.InvalidServiceVersion, "A Kaleido HTTP service must name the version in its published routes.");
        var sourceName = item.Name.ToLowerInvariant();

        return new QueryableSourceResponse
        {
            ServiceName = serviceName,
            ServiceVersion = serviceOptions.ServiceVersion,
            Name = item.Name,
            Description = item.Description,
            DisplayName = item.DisplayName,
            Source = item.Source,
            Pageable = item.Pageable.ToWire(),
            Authorization = item.Authorization,
            QueryUrl = QueryableContractUrls.QuerySourceQuery(serviceName, version, sourceName),
            Fields = [.. item.Fields.Select(f => f.ToWire())],
            Parameters = [.. item.Parameters.Select(p => p.ToWire())],
            OutputFields = [.. item.OutputFields.Select(o => o.ToWire())],
            Views = [.. item.Views.Select(view => view.ToResponse(sourceName, serviceName, version))],
        };
    }

    public static QueryableViewResponse ToResponse(
        this QueryableViewRegistryItem item,
        string sourceName,
        string serviceName,
        string serviceVersion)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        return new QueryableViewResponse
        {
            Name = item.Name,
            Description = item.Description,
            DisplayName = item.DisplayName,
            Pageable = item.Pageable.ToWire(),
            Authorization = item.Authorization,
            QueryUrl = QueryableContractUrls.QueryViewQuery(serviceName, serviceVersion, sourceName, item.Name.ToLowerInvariant()),
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

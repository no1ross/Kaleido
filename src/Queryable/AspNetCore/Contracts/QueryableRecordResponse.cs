using Kaleido.Queryable.Metadata;
using System.Net.Mime;
using System.Reflection;

namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record QueryableRecordResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public string? Source { get; init; }

    public required string MetadataUrl { get; init; }

    public string? QueryUrl { get; init; }

    public IReadOnlyCollection<QueryableFieldMetadata> Fields { get; init; }
        = Array.Empty<QueryableFieldMetadata>();

    public IReadOnlyCollection<QueryableViewResponse> Views { get; init; }
        = Array.Empty<QueryableViewResponse>();

    public static QueryableRecordResponse FromRegistration(
        QueryContextRegistration registration,
        IReadOnlyCollection<QueryViewRegistration> views,
        QueryableRouteOptions options) =>
        FromMetadata(
            registration.Metadata,
            views.Select(view => new QueryableViewDefinition(
                view.ViewType,
                view.Metadata)),
            options,
            includeDirectQueryUrl: registration.Metadata.Kind == QueryContextKind.Direct);

    public static QueryableRecordResponse FromDelegatedRegistration(
        QueryContextMetadata metadata,
        IReadOnlyCollection<DelegatedQueryViewRegistration> views,
        QueryableRouteOptions options) =>
        FromMetadata(
            metadata,
            views.Select(view => new QueryableViewDefinition(
                view.ViewType,
                view.ViewMetadata)),
            options,
            includeDirectQueryUrl: false);

    public static QueryableRecordSummary ToSummary(
        QueryContextRegistration registration,
        QueryableRouteOptions options) =>
        ToSummary(
            registration.Metadata,
            options);

    public static QueryableRecordSummary ToSummary(
        QueryContextMetadata metadata,
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);

        var contextName =
            metadata.Name.ToLowerInvariant();

        return new QueryableRecordSummary
        {
            Name = metadata.Name,

            Description = metadata.Description,

            MetadataUrl =
                QueryableContractUrls.QueryContextMetadata(
                    options,
                    contextName)
        };
    }

    private static QueryableRecordResponse FromMetadata(
        QueryContextMetadata metadata,
        IEnumerable<QueryableViewDefinition> views,
        QueryableRouteOptions options,
        bool includeDirectQueryUrl)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);

        var contextName =
            metadata.Name.ToLowerInvariant();

        return new QueryableRecordResponse
        {
            Name = metadata.Name,
            Description = metadata.Description,
            DisplayName = metadata.DisplayName,
            Version = metadata.Version,
            Source = metadata.Source,
            MetadataUrl = QueryableContractUrls.QueryContextMetadata(options, contextName),
            QueryUrl = includeDirectQueryUrl
                ? QueryableContractUrls.QueryContextQuery(options, contextName)
                : null,
            Fields = metadata.Fields
                .Select(QueryableFieldMetadata.FromMetadata)
                .ToArray(),
            Views = views
                .Where(view => view.Metadata.Visibility == QueryViewVisibility.Public)
                .OrderBy(view => view.Metadata.Name)
                .Select(view =>
                {
                    var viewName = view.Metadata.Name.ToLowerInvariant();

                    return new QueryableViewResponse
                    {
                        Name = view.Metadata.Name,
                        Description = view.Metadata.Description,
                        DisplayName = view.Metadata.DisplayName,
                        Pageable = view.Metadata.Pageable is null
                            ? null
                            : PageableContract.FromMetadata(view.Metadata.Pageable),
                        QueryUrl = QueryableContractUrls.QueryViewQuery(
                            options,
                            contextName,
                            viewName),
                        Parameters = view.Metadata.Parameters is null
                            ? null
                            : view.Metadata.Parameters
                                .Select(QueryableQueryParameter.FromMetadata)
                                .ToArray(),
                        Fields = view.ViewType
                            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Select(QueryableQueryProperty.FromPropertyInfo)
                            .ToArray()
                    };
                })
                .ToArray()
        };
    }

    private sealed record QueryableViewDefinition(
        Type ViewType,
        QueryViewMetadata Metadata);
}


public sealed record QueryableViewResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public PageableContract? Pageable { get; init; }

    public required string QueryUrl { get; init; }

    public IReadOnlyCollection<QueryableQueryParameter>? Parameters { get; init; }

    public IReadOnlyCollection<QueryableQueryProperty>? Fields { get; init; }
}

public sealed record QueryableQueryProperty
{
    public required string Name { get; init; }

    public required DataTypeDescriptor DataType { get; init; }


    public static QueryableQueryProperty FromPropertyInfo(PropertyInfo propertyInfo)
    {
        return new QueryableQueryProperty
        {
            Name = propertyInfo.Name,
            DataType = DataTypeMapper.GetDescriptor(propertyInfo.PropertyType)            
        };
    }
}

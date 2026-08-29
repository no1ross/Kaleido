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
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var contextName =
            registration.Metadata.Name.ToLowerInvariant();

        return new QueryableRecordResponse
        {
            Name = registration.Metadata.Name,

            Description = registration.Metadata.Description,

            DisplayName = registration.Metadata.DisplayName,

            Version = registration.Metadata.Version,

            Source = registration.Metadata.Source,

            MetadataUrl =
                QueryableContractUrls.QueryContextMetadata(
                    options,
                    contextName),

            QueryUrl =
                registration.Metadata.AllowDirectQuery
                    ? QueryableContractUrls.QueryContextQuery(
                        options,
                        contextName)
                    : null,

            Fields = registration.Metadata.Fields
                .Select(QueryableFieldMetadata.FromMetadata)
                .ToArray(),

            Views = views
                .Where(view => view.Metadata.Visibility == QueryViewVisibility.Public)
                .OrderBy(v => v.Metadata.Name)
                .Select(view =>
                {
                    var viewName =
                        view.Metadata.Name.ToLowerInvariant();

                    return new QueryableViewResponse
                    {
                        Name = view.Metadata.Name,

                        Description = view.Metadata.Description,

                        DisplayName = view.Metadata.DisplayName,

                        Pageable =
                            view.Metadata.Pageable is null
                                ? null
                                : PageableContract.FromMetadata(
                                    view.Metadata.Pageable),

                        QueryUrl =
                            QueryableContractUrls.QueryViewQuery(
                                options,
                                contextName,
                                viewName),

                        Parameters = view.Metadata.Parameters is null 
                            ? null 
                            : view.Metadata.Parameters
                                .Select(x => QueryableQueryParameter.FromMetadata(x)).ToArray(),

                        Fields = view.ViewType
                                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Select(QueryableQueryProperty.FromPropertyInfo)
                                    .ToArray()

                    };
                })
                .ToArray()
        };
    }

    public static QueryableRecordSummary ToSummary(
        QueryContextRegistration registration,
        QueryableRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var contextName =
            registration.Metadata.Name.ToLowerInvariant();

        return new QueryableRecordSummary
        {
            Name = registration.Metadata.Name,

            Description = registration.Metadata.Description,

            MetadataUrl =
                QueryableContractUrls.QueryContextMetadata(
                    options,
                    contextName)
        };
    }
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

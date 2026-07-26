using Kaleido;
using Kaleido.Queryable.AspNetCore;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Metadata;
using Microsoft.Extensions.Options;

namespace Kaleido.Queryable.AspNetCore.Contracts
{
    public sealed record RecordContract
    {
        public required string Name { get; init; }

        public string? Description { get; init; }

        public string? Version { get; init; }

        public string? Source { get; init; }

        public PageableContract? Pageable { get; init; }

        public IReadOnlyCollection<FieldContract> Fields { get; init; }
            = Array.Empty<FieldContract>();

        public IReadOnlyCollection<NamedQuerySummaryContract> NamedQueries { get; init; }
            = Array.Empty<NamedQuerySummaryContract>();

        public static RecordContract FromRegistration(
            RecordRegistration registration,
            QueryableAspNetCoreOptions options)
        {
            ArgumentNullException.ThrowIfNull(registration);

            return new RecordContract
            {
                Name = registration.Metadata.Name,
                Description = registration.Metadata.Description,
                Version = registration.Metadata.Version,
                Source = registration.Metadata.Source,
                Pageable = registration.Metadata.Pageable is null
                    ? null
                    : PageableContract.FromMetadata(
                        registration.Metadata.Pageable),
                Fields = registration.Metadata.Fields
                    .Select(FieldContract.FromMetadata)
                    .ToArray(),
                NamedQueries = registration.NamedQueryTypes
                .OrderBy(q => q.Metadata.Name)
                .Select(q =>
                {
                    var queryName =
                        q.Metadata.Name.ToLowerInvariant();

                    var recordName =
                        registration.Metadata.Name.ToLowerInvariant();

                    return new NamedQuerySummaryContract
                    {
                        Name = q.Metadata.Name,

                        Description = q.Metadata.Description,

                        ExecuteUrl = QueryableContractUrls.NamedQuery(
                            options,
                            recordName,
                            queryName),

                        MetadataUrl = QueryableContractUrls.NamedQueryMetadata(
                            options,
                            recordName,
                            queryName)
                    };
                })
                .ToArray()
            };
        }
        public static RecordSummaryContract ToSummary(
            RecordRegistration registration,
            QueryableAspNetCoreOptions options)
        {
            ArgumentNullException.ThrowIfNull(registration);
            ArgumentNullException.ThrowIfNull(options);

            var routePrefix =
                options.RoutePrefix.TrimEnd('/');

            var recordName =
                registration.Metadata.Name.ToLowerInvariant();

            var recordBase =
                $"{routePrefix}/{recordName}";

            return new RecordSummaryContract
            {
                Name = registration.Metadata.Name,

                Description = registration.Metadata.Description,

                MetadataUrl = QueryableContractUrls.RecordMetadata(options, recordName),

                QueryUrl = QueryableContractUrls.RecordQuery(options, recordName),

                NamedQueries = registration.NamedQueryTypes
                .OrderBy(q => q.Metadata.Name)
                .Select(q =>
                {
                    var queryName = q.Metadata.Name.ToLowerInvariant();

                    return new NamedQuerySummaryContract
                    {
                        Name = q.Metadata.Name,
                        Description = q.Metadata.Description,
                        ExecuteUrl = QueryableContractUrls.NamedQuery(
                            options,
                            recordName,
                            queryName),
                        MetadataUrl = QueryableContractUrls.NamedQueryMetadata(
                            options,
                            recordName,
                            queryName)
                    };
                })
                .ToArray()
            };
        }

    }
}

public sealed record FieldContract
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

    public static FieldContract FromMetadata(
        FieldMetadata metadata)
    {
        return new FieldContract
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

public sealed record PageableContract
{
    public int DefaultSize { get; init; }

    public int MaxSize { get; init; }

    public static PageableContract FromMetadata(
        PageableMetadata metadata)
    {
        return new PageableContract
        {
            DefaultSize = metadata.DefaultSize,
            MaxSize = metadata.MaxSize
        };
    }
}

public sealed record NamedQueryContract
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyCollection<QueryParameterContract> Parameters { get; init; }
        = Array.Empty<QueryParameterContract>();

    public static NamedQueryContract FromRegistration(
        NamedQueryRegistration registration)
    {
        return new NamedQueryContract
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            Parameters = registration.Metadata.Parameters?
                .Select(QueryParameterContract.FromMetadata)
                .ToArray()
                ?? Array.Empty<QueryParameterContract>()
        };
    }
}

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

public sealed record RecordSummaryContract
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? MetadataUrl { get; init; }

    public string? QueryUrl { get; init; }

    public IReadOnlyCollection<NamedQuerySummaryContract>? NamedQueries { get; init; }
}

public sealed record NamedQuerySummaryContract
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string ExecuteUrl { get; init; }

    public required string MetadataUrl { get; init; }
}

internal static class QueryableContractMapper
{
}

//internal static class QueryableRoutes
//{
//    public static string RecordBase(
//        QueryableAspNetCoreOptions options,
//        string recordName)
//        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}";

//    public static string RecordMetadata(
//        QueryableAspNetCoreOptions options,
//        string recordName)
//        => $"{RecordBase(options, recordName)}/{options.MetadataRoute}";

//    public static string RecordQuery(
//        QueryableAspNetCoreOptions options,
//        string recordName)
//        => $"{RecordBase(options, recordName)}/{options.QueryRoute}";

//    public static string NamedQuery(
//        QueryableAspNetCoreOptions options,
//        string recordName,
//        string queryName)
//        => $"{RecordBase(options, recordName)}/{options.QueriesRoute}/{queryName}";

//    public static string NamedQueryMetadata(
//        QueryableAspNetCoreOptions options,
//        string recordName,
//        string queryName)
//        => $"{NamedQuery(options, recordName, queryName)}/{options.MetadataRoute}";
//}

internal static class QueryableEndpointNames
{
    public static string CatalogEndpointName =>
        "queryable-catalog";

    public static string RecordMetadataEndpointName(
        string recordName)
        => $"{recordName}-metadata";

    public static string RecordQueryEndpointName(
        string recordName)
        => $"{recordName}-query";

    public static string NamedQueryEndpointName(
        string recordName,
        string queryName)
        => $"{recordName}-{queryName}";

    public static string NamedQueryMetadataEndpointName(
        string recordName,
        string queryName)
        => $"{recordName}-{queryName}-metadata";
}

internal static class QueryableRoutePaths
{
    public static string RecordMetadata(
        QueryableAspNetCoreOptions options,
        string recordName)
        => $"{recordName}/{options.MetadataRoute}";

    public static string RecordQuery(
        QueryableAspNetCoreOptions options,
        string recordName)
        => $"{recordName}/{options.QueryRoute}";

    public static string NamedQuery(
        QueryableAspNetCoreOptions options,
        string recordName,
        string queryName)
        => $"{recordName}/{options.QueriesRoute}/{queryName}";

    public static string NamedQueryMetadata(
        QueryableAspNetCoreOptions options,
        string recordName,
        string queryName)
        => $"{recordName}/{options.QueriesRoute}/{queryName}/{options.MetadataRoute}";
}

internal static class QueryableContractUrls
{
    public static string RecordMetadata(
        QueryableAspNetCoreOptions options,
        string recordName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}/{options.MetadataRoute}";

    public static string RecordQuery(
        QueryableAspNetCoreOptions options,
        string recordName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}/{options.QueryRoute}";

    public static string NamedQuery(
        QueryableAspNetCoreOptions options,
        string recordName,
        string queryName)
        => $"{options.RoutePrefix.TrimEnd('/')}/{recordName}/{options.QueriesRoute}/{queryName}";

    public static string NamedQueryMetadata(
        QueryableAspNetCoreOptions options,
        string recordName,
        string queryName)
        => $"{NamedQuery(options, recordName, queryName)}/{options.MetadataRoute}";
}
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// Provides endpoint registration extensions for Kaleido Queryable.
/// </summary>
public static class QueryableEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapQueryable(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var registry =
            endpoints.ServiceProvider
                .GetRequiredService<IRecordRegistry>();

        var options =
            endpoints.ServiceProvider
                .GetRequiredService<IOptions<QueryableRouteOptions>>()
                .Value;

        var group =
            endpoints.MapGroup(options.RoutePrefix);

        group.MapGet(
                "",
                () => Results.Ok(
                    registry.Registrations
                        .Select(r =>
                            QueryableRecordResponse.ToSummary(
                                r,
                                options))
                        .OrderBy(r => r.Name)))
            .WithName(QueryableEndpointNames.CatalogEndpointName)
            .WithTags("Queryable Records")
            .WithSummary("Get registered records.")
            .WithDescription(
                "Returns all registered queryable records. " +
                "Each record provides links to retrieve metadata, execute ad hoc queries, " +
                "and execute named queries.")
            .Produces<IReadOnlyCollection<QueryableRecordSummary>>();

        foreach (var record in registry.Registrations)
        {
            group.MapRecord(
                record,
                options);
        }

        return endpoints;
    }

    public static void MapRecord(
        this IEndpointRouteBuilder endpoints,
        RecordRegistration record,
        QueryableRouteOptions options)
    {
        var recordName =
            record.Metadata.Name.ToLowerInvariant();

        endpoints.MapMetadataEndpoint(
            record,
            QueryableRoutePaths.RecordMetadata(
                options,
                recordName),
            options);

        endpoints.MapQueryEndpoint(
            record,
            QueryableRoutePaths.RecordQuery(
                options,
                recordName));

        foreach (var namedQuery in record.NamedQueryTypes)
        {
            var queryName =
                namedQuery.Metadata.Name.ToLowerInvariant();

            var route =
                QueryableRoutePaths.NamedQuery(
                    options,
                    recordName,
                    queryName);

            endpoints.MapNamedQueryEndpoint(
                record,
                namedQuery,
                route);

            endpoints.MapNamedQueryMetadataEndpoint(
                record,
                namedQuery,
                route);
        }
    }

    private static void MapMetadataEndpoint(
        this IEndpointRouteBuilder endpoints,
        RecordRegistration record,
        string route,
        QueryableRouteOptions options)
    {
        endpoints.MapGet(
                route,
                () => Results.Ok(
                    QueryableRecordResponse.FromRegistration(
                        record,
                        options)))
            .WithName(
                QueryableEndpointNames.RecordMetadataEndpointName(
                    record.Metadata.Name.ToLowerInvariant()))
            .WithTags(
                record.Metadata.DisplayName)
            .WithSummary(
                $"Get metadata for {record.Metadata.DisplayName}.")
            .WithDescription(
                $"Returns metadata describing the '{record.Metadata.DisplayName}' record type, including fields, data types, query capabilities, and available named queries.")
            .Produces<QueryableRecordResponse>();
    }

    private static void MapNamedQueryMetadataEndpoint(
        this IEndpointRouteBuilder endpoints,
        RecordRegistration record,
        NamedQueryRegistration namedQuery,
        string route)
    {
        endpoints.MapGet(
                route,
                () => Results.Ok(
                    QueryableNamedQuery.FromRegistration(
                        namedQuery)))
            .WithName(
                QueryableEndpointNames.NamedQueryMetadataEndpointName(
                    record.Metadata.Name.ToLowerInvariant(),
                    namedQuery.Metadata.Name.ToLowerInvariant()))
            .WithTags($"{record.Metadata.DisplayName} - {namedQuery.Metadata.DisplayName}")
            .WithSummary(
                $"Get metadata for {namedQuery.Metadata.DisplayName}.")
            .WithDescription(
                $"Returns metadata describing the '{namedQuery.Metadata.DisplayName}' named query, including parameters, supported values, and execution requirements.")
            .Produces<QueryableNamedQuery>();
    }

    private static void MapQueryEndpoint(
        this IEndpointRouteBuilder endpoints,
        RecordRegistration record,
        string route)
    {
        typeof(QueryableEndpointRouteBuilderExtensions)
            .GetMethod(
                nameof(MapTypedQueryEndpoint),
                BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(record.RecordType)
            .Invoke(
                null,
                new object[]
                {
                    endpoints,
                    route,
                    record
                });
    }

    private static void MapNamedQueryEndpoint(
        this IEndpointRouteBuilder endpoints,
        RecordRegistration record,
        NamedQueryRegistration namedQuery,
        string route)
    {
        typeof(QueryableEndpointRouteBuilderExtensions)
            .GetMethod(
                nameof(MapTypedNamedQueryEndpoint),
                BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(record.RecordType)
            .Invoke(
                null,
                new object[]
                {
                    endpoints,
                    route,
                    record,
                    namedQuery
                });
    }

    private static void MapTypedQueryEndpoint<TRecord>(
        IEndpointRouteBuilder endpoints,
        string route,
        RecordRegistration record)
        where TRecord : class
    {
        var recordKey =
            record.Metadata.Name;

        endpoints.MapPost(
                route,
                async (
                    QueryApiRequest request,
                    IQueryableService catalog,
                    CancellationToken cancellationToken) =>
                {
                    var query =
                        QueryableValueNormalizer.Normalize(
                            request.Query,
                            record.Metadata);

                    var result =
                        await catalog.QueryAsync<TRecord>(
                            recordKey,
                            new QueryRequest(
                                Query: query),
                            cancellationToken);

                    return Results.Ok(result);
                })
            .WithName(
                QueryableEndpointNames.RecordQueryEndpointName(
                    recordKey.ToLowerInvariant()))
            .WithTags(
                record.Metadata.DisplayName)
            .WithSummary(
                $"Query {record.Metadata.DisplayName}.")
            .WithDescription(
                $"Executes an ad hoc query against the '{record.Metadata.DisplayName}' record type.")
            .Accepts<QueryApiRequest>(
                "application/json")
            .Produces<QueryResult<TRecord>>();
    }

    private static void MapTypedNamedQueryEndpoint<TRecord>(
        IEndpointRouteBuilder endpoints,
        string route,
        RecordRegistration record,
        NamedQueryRegistration namedQuery)
        where TRecord : class
    {
        var queryName =
            namedQuery.Metadata.Name;

        endpoints.MapPost(
                route,
                async (
                    HttpRequest httpRequest,
                    NamedQueryApiRequest request,
                    IQueryableService catalog,
                    CancellationToken cancellationToken) =>
                {
                    var parameters =
                        QueryableValueNormalizer.Normalize(
                            GetParameters(
                                httpRequest,
                                request),
                            namedQuery.Metadata.Parameters);

                    var result =
                        await catalog.QueryAsync<TRecord>(
                            record.Metadata.Name,
                            new QueryRequest(
                                NamedQuery: new NamedQuery(
                                    queryName,
                                    parameters)),
                            cancellationToken);

                    return Results.Ok(result);
                })
            .WithName(
                QueryableEndpointNames.NamedQueryEndpointName(
                    record.Metadata.Name.ToLowerInvariant(),
                    queryName.ToLowerInvariant()))
            .WithTags($"{record.Metadata.DisplayName} - {namedQuery.Metadata.DisplayName}")
            .WithSummary(
                $"Execute {namedQuery.Metadata.DisplayName}.")
            .WithDescription(namedQuery.Metadata.Description)
            .Accepts<NamedQueryApiRequest>(
                "application/json")
            .Produces<QueryResult<TRecord>>();
    }

    private static IReadOnlyDictionary<string, object?> GetParameters(
        HttpRequest request,
        NamedQueryApiRequest body)
    {
        if (body.Values?.Count > 0)
        {
            return body.Values;
        }

        return request.Query
            .ToDictionary(
                x => x.Key,
                x => (object?)x.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);
    }
}

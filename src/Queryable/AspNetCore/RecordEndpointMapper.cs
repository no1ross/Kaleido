using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace Kaleido.Queryable.AspNetCore;

internal static class RecordEndpointMapper
{
    public static void MapRecord(
        this IEndpointRouteBuilder endpoints,
        RecordRegistration record,
        QueryableAspNetCoreOptions options)
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

            endpoints.MapNamedQueryEndpoint(
                record,
                namedQuery,
                QueryableRoutePaths.NamedQuery(
                    options,
                    recordName,
                    queryName));

            endpoints.MapNamedQueryMetadataEndpoint(
                record,
                namedQuery,
                QueryableRoutePaths.NamedQueryMetadata(
                    options,
                    recordName,
                    queryName));
        }
    }

    private static void MapMetadataEndpoint(
        this IEndpointRouteBuilder endpoints,
        RecordRegistration record,
        string route,
        QueryableAspNetCoreOptions options)
    {
        endpoints.MapGet(
                route,
                () => Results.Ok(
                    RecordContract.FromRegistration(record, options)))
            .WithName(
                $"{QueryableEndpointNames.RecordMetadataEndpointName(record.Metadata.Name.ToLowerInvariant())}")
            .WithTags(record.Metadata.Name);
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
                    NamedQueryContract.FromRegistration(namedQuery)))
            .WithName(
                $"{QueryableEndpointNames.NamedQueryMetadataEndpointName(record.Metadata.Name.ToLowerInvariant(), namedQuery.Metadata.Name.ToLowerInvariant())}")
            .WithTags(record.Metadata.Name);
    }

    private static void MapQueryEndpoint(
        this IEndpointRouteBuilder endpoints,
        RecordRegistration record,
        string route)
    {
        typeof(RecordEndpointMapper)
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
        typeof(RecordEndpointMapper)
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
                    record.Metadata.Name,
                    namedQuery
                });
    }

    private static void MapTypedQueryEndpoint<TRecord>(
        IEndpointRouteBuilder endpoints,
        string route,
        RecordRegistration record)
        where TRecord : class
    {
        var recordKey = record.Metadata.Name;
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
                $"{QueryableEndpointNames.RecordQueryEndpointName(recordKey.ToLowerInvariant())}")
            .WithTags(recordKey);
    }

    private static void MapTypedNamedQueryEndpoint<TRecord>(
        IEndpointRouteBuilder endpoints,
        string route,
        string recordKey,
        NamedQueryRegistration namedQuery)
        where TRecord : class
    {
        var queryName = namedQuery.Metadata.Name;
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
                            recordKey,
                            new QueryRequest(
                                NamedQuery: new NamedQuery(
                                    queryName,
                                    parameters)),
                            cancellationToken);

                    return Results.Ok(result);
                })
            .WithName(
                $"{QueryableEndpointNames.NamedQueryEndpointName(recordKey.ToLowerInvariant(), queryName.ToLowerInvariant())}")
            .WithTags(recordKey);
    }

    private static IReadOnlyDictionary<string, object?> GetParameters(HttpRequest request, NamedQueryApiRequest body)
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

using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// Provides endpoint registration extensions for Kaleido Queryable.
/// </summary>
public static class QueryableEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapQueryable(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var contextRegistry =
            endpoints.ServiceProvider
                .GetRequiredService<IQueryContextRegistry>();

        var viewRegistry =
            endpoints.ServiceProvider
                .GetRequiredService<IQueryViewRegistry>();

        var options =
            endpoints.ServiceProvider
                .GetRequiredService<IOptions<QueryableRouteOptions>>()
                .Value;

        var group =
            endpoints.MapGroup(options.RoutePrefix);

        group.MapGet(
                "",
                () => Results.Ok(
                    contextRegistry.Registrations
                        .Select(r =>
                            QueryableRecordResponse.ToSummary(
                                r,
                                options))
                        .OrderBy(r => r.Name)))
            .WithName(
                QueryableEndpointNames.CatalogEndpointName)
            .WithTags("Queryable")
            .WithSummary(
                "Get registered query contexts.")
            .WithDescription(
                "Returns all registered query contexts. " +
                "Each context provides links to retrieve metadata, " +
                "discover available views, and execute queries through those views.")
            .Produces<IReadOnlyCollection<QueryableRecordSummary>>();

        foreach (var context in contextRegistry.Registrations)
        {
            group.MapMetadataEndpoint(
                context,
                QueryableRoutePaths.QueryContextMetadata(
                    options,
                    context.Metadata.Name.ToLowerInvariant()),
                options);
        }

        foreach (var view in viewRegistry.Registrations)
        {
            group.MapQueryView(
                contextRegistry,
                view,
                options);
        }

        return endpoints;
    }

    public static void MapQueryView(
      this IEndpointRouteBuilder endpoints,
      IQueryContextRegistry contextRegistry,
      QueryViewRegistration view,
      QueryableRouteOptions options)
    {
        var context =
            contextRegistry.GetRegistration(
                view.QueryContextType);

        var contextName =
            context.Metadata.Name.ToLowerInvariant();

        var viewName =
            view.Metadata.Name.ToLowerInvariant();

        endpoints.MapQueryEndpoint(
            context,
            view,
            QueryableRoutePaths.QueryViewQuery(
                options,
                contextName,
                viewName));
    }

    private static void MapMetadataEndpoint(
        this IEndpointRouteBuilder endpoints,
        QueryContextRegistration context,
        string route,
        QueryableRouteOptions options)
    {
        endpoints.MapGet(
                route,
                () => Results.Ok(
                    QueryableRecordResponse.FromRegistration(
                        context,
                        options)))
            .WithName(
                QueryableEndpointNames.QueryContextMetadataEndpointName(
                    context.Metadata.Name.ToLowerInvariant()))
            .WithTags(
                context.Metadata.DisplayName)
            .WithSummary(
                $"Get metadata for {context.Metadata.DisplayName}.")
            .WithDescription(
                $"Returns metadata describing the '{context.Metadata.DisplayName}' query context, including fields, data types, query capabilities, available views, and available named queries.")
            .Produces<QueryableRecordResponse>();
    }

    private static void MapQueryEndpoint(
        this IEndpointRouteBuilder endpoints,
        QueryContextRegistration context,
        QueryViewRegistration view,
        string route)
    {
        typeof(QueryableEndpointRouteBuilderExtensions)
            .GetMethod(
                nameof(MapTypedQueryEndpoint),
                BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(
                view.QueryViewType,
                view.ViewType,
                view.ViewParametersType)
            .Invoke(
                null,
                new object[]
                {
                    endpoints,
                    route,
                    context,
                    view
                });
    }

    private static void MapTypedQueryEndpoint<TQueryView, TView, TViewParameters>(
        IEndpointRouteBuilder endpoints,
        string route,
        QueryContextRegistration context,
        QueryViewRegistration view)
        where TQueryView : class
        where TView : class
        where TViewParameters : class
    {
        endpoints.MapPost(
                route,
                async (
                    QueryApiRequest<TViewParameters> request,
                    IQueryableService queryable,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var query =
                            QueryableValueNormalizer.Normalize(
                                request.Query,
                                context.Metadata);

                        var result =
                            await queryable.QueryAsync<TQueryView, TView>(
                                new QueryRequest<TViewParameters>(
                                    Query: query,
                                    ViewParameters: request.Parameters),
                                cancellationToken);

                        return Results.Ok(result);
                    }
                    catch (QueryableValidationException ex)
                    {
                        return Results.BadRequest(
                            new QueryErrorResponse(
                            [
                                new QueryError(
                                ex.Code,
                                ex.Message)
                            ]));
                    }
                })
            .WithName(
                QueryableEndpointNames.QueryViewEndpointName(
                    context.Metadata.Name.ToLowerInvariant(),
                    view.Metadata.Name.ToLowerInvariant()))
            .WithTags(
                $"{context.Metadata.DisplayName} - {view.Metadata.DisplayName}")
            .WithSummary(
                $"Query {view.Metadata.DisplayName}.")
            .WithDescription(
                $"Executes a query against the '{view.Metadata.DisplayName}' view.")
            .Accepts<QueryApiRequest>(
                "application/json")
            .Produces<QueryResult<TView>>()
            .Produces<QueryErrorResponse>(400);
    }
}

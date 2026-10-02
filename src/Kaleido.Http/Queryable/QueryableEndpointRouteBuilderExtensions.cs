using System.Reflection;
using Kaleido.Http.Authorization;
using Kaleido.Queryable.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Queryable;

/// <summary>
/// Provides endpoint registration extensions for Kaleido Queryable.
/// </summary>
public static class QueryableEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all Kaleido Queryable endpoints and returns the route group so hosts can
    /// compose conventions (e.g. <c>.RequireAuthorization()</c>) onto every endpoint.
    /// </summary>
    internal static RouteGroupBuilder MapQueryable(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var queryableRegistry =
            endpoints.ServiceProvider
                .GetService<IQueryableRegistry>();

        if (queryableRegistry is null)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.QryInvalidRegistration,
                "Cannot map Queryable endpoints: Queryable runtime is not registered. " +
                "Use MapKaleidoHttp() to map Kaleido endpoints.");
        }

        var httpOptions =
            endpoints.ServiceProvider
                .GetRequiredService<KaleidoHttpOptions>();

        var serviceName =
            endpoints.ServiceProvider
                .GetRequiredService<KaleidoServiceOptions>()
                .ServiceName;

        var logger =
            endpoints.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Kaleido.Queryable.Startup");

        var group =
            endpoints.MapGroup(
                QueryableContractUrls.QueryablePrefix(serviceName))
            .AddEndpointFilter<KaleidoJsonEndpointFilter>()
            .AddEndpointFilter<KaleidoCallerContextEndpointFilter>();

        var viewCount = queryableRegistry.Registrations.Sum(c => c.Views.Count);

        logger.LogInformation(
            "Queryable mapped at route prefix {RoutePrefix} with {QueryContextCount} query contexts and {QueryViewCount} views.",
            QueryableContractUrls.QueryablePrefix(serviceName),
            queryableRegistry.Registrations.Count,
            viewCount);

        group.MapGet(
                "",
                async (
                    HttpContext httpContext,
                    [FromServices] IKaleidoAuthorizer authorizer,
                    CancellationToken cancellationToken) =>
                    Results.Ok(
                        (await authorizer.FilterAsync(queryableRegistry.Registrations,
                                r => r.Authorization,
                                cancellationToken))
                            .Select(r =>
                                QueryableRecordResponse.ToSummary(
                                    r,
                                    serviceName))
                            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)))
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

        group.MapGet(
            "registry",
            async (
                HttpContext httpContext,
                [FromServices] IKaleidoAuthorizer authorizer,
                CancellationToken cancellationToken) =>
            {
                var allowedContexts =
                    await authorizer.FilterAsync(queryableRegistry.Registrations,
                        r => r.Authorization,
                        cancellationToken);

                var records = new List<QueryableRecordResponse>();

                foreach (var context in allowedContexts)
                {
                    var views =
                        await authorizer.FilterAsync(context.Views,
                            v => v.Authorization,
                            cancellationToken);

                    records.Add(
                        QueryableRecordResponse.FromRegistryItem(
                            context with { Views = views },
                            serviceName));
                }

                return Results.Ok(
                    records.OrderBy(
                        r => r.Name,
                        StringComparer.OrdinalIgnoreCase));
            })
                .WithName(
                    QueryableEndpointNames.RegistryEndpointName)
                .WithTags("Queryable")
                .WithSummary(
                    "Get queryable registry metadata.")
                .WithDescription(
                    "Returns the complete metadata registry for all registered query contexts and views. " +
                    "This endpoint is intended for consumer registry initialization and provides the information " +
                    "required to discover available views, resolve query endpoints, understand query parameters, " +
                    "and identify supported search, filter, sort, and paging capabilities.")
                .Produces<IReadOnlyCollection<QueryableRecordResponse>>();

        foreach (var context in queryableRegistry.Registrations)
        {
            group.MapMetadataEndpoint(
                context,
                QueryableRoutePaths.QueryContextMetadata(
                    context.Name.ToLowerInvariant()),
                serviceName,
                httpOptions);

            if (context.Kind == QueryContextKind.Direct)
            {
                group.MapDirectQueryContext(context, httpOptions);
            }

            foreach (var view in context.Views)
            {
                group.MapQueryView(context, view, httpOptions);
            }
        }

        return group;
    }

    private static void MapQueryView(
        this IEndpointRouteBuilder endpoints,
        QueryableContextRegistryItem context,
        QueryableViewRegistryItem view,
        KaleidoHttpOptions options)
    {
        var contextName = context.Name.ToLowerInvariant();
        var viewName = view.Name.ToLowerInvariant();

        endpoints.MapQueryEndpoint(
            context,
            view,
            QueryableRoutePaths.QueryViewQuery(contextName, viewName),
            options);
    }

    private static void MapDirectQueryContext(
        this IEndpointRouteBuilder endpoints,
        QueryableContextRegistryItem context,
        KaleidoHttpOptions options)
    {
        var method = typeof(QueryableEndpointRouteBuilderExtensions)
            .GetMethod(
                nameof(MapTypedDirectQueryEndpoint),
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                $"Method '{nameof(MapTypedDirectQueryEndpoint)}' not found.");

        method
            .MakeGenericMethod(context.ContextType)
            .Invoke(
                null,
                [
                    endpoints,
                    QueryableRoutePaths.QueryContextQuery(context.Name.ToLowerInvariant()),
                    context,
                    options
                ]);
    }

    private static void MapMetadataEndpoint(
        this IEndpointRouteBuilder endpoints,
        QueryableContextRegistryItem context,
        string route,
        string serviceName,
        KaleidoHttpOptions options)
    {
        endpoints.MapGet(
                route,
                async (
                    HttpContext httpContext,
                    [FromServices] IKaleidoAuthorizer authorizer,
                    CancellationToken cancellationToken) =>
                    Results.Ok(
                        QueryableRecordResponse.FromRegistryItem(
                            context with
                            {
                                Views =
                                    await authorizer.FilterAsync(context.Views,
                                        v => v.Authorization,
                                        cancellationToken)
                            },
                            serviceName)))
            .WithKaleidoAuthorization(context.Authorization, options)
            .WithName(
                QueryableEndpointNames.QueryContextMetadataEndpointName(
                    context.Name.ToLowerInvariant()))
            .WithTags(
                context.DisplayName ?? context.Name)
            .WithSummary(
                $"Get metadata for {context.DisplayName ?? context.Name}.")
            .WithDescription(
                $"Returns metadata describing the '{context.DisplayName ?? context.Name}' query context, including fields, data types, query capabilities, available views, and available named queries.")
            .Produces<QueryableRecordResponse>();
    }

    private static void MapQueryEndpoint(
        this IEndpointRouteBuilder endpoints,
        QueryableContextRegistryItem context,
        QueryableViewRegistryItem view,
        string route,
        KaleidoHttpOptions options)
    {
        var method = typeof(QueryableEndpointRouteBuilderExtensions)
            .GetMethod(
                nameof(MapTypedQueryEndpoint),
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                $"Method '{nameof(MapTypedQueryEndpoint)}' not found.");

        method
            .MakeGenericMethod(
                view.QueryViewType,
                view.ViewType,
                view.ViewParametersType)
            .Invoke(
                null,
                [endpoints, route, context, view, options]);
    }

    private static void MapTypedQueryEndpoint<TQueryView, TView, TViewParameters>(
        IEndpointRouteBuilder endpoints,
        string route,
        QueryableContextRegistryItem context,
        QueryableViewRegistryItem view,
        KaleidoHttpOptions options)
        where TQueryView : class
        where TView : class
        where TViewParameters : class
    {
        var fields = context.Fields;

        endpoints.MapPost(
                route,
                async (
                    QueryApiRequest<TViewParameters> request,
                    IQueryableService queryable,
                    CancellationToken cancellationToken) =>
                    Results.Ok(
                        await queryable.QueryAsync<TQueryView, TView>(
                            new QueryRequest<TViewParameters>(
                                Query: request.Query.ToQueryBody(fields),
                                ViewParameters: request.Parameters),
                            cancellationToken)))
            .WithKaleidoAuthorization(view.Authorization, options)
            .WithName(
                QueryableEndpointNames.QueryViewEndpointName(
                    context.Name.ToLowerInvariant(),
                    view.Name.ToLowerInvariant()))
            .WithTags(
                $"{context.DisplayName} - {view.DisplayName}")
            .WithSummary(
                $"Query {view.DisplayName}.")
            .WithDescription(
                $"Executes a query against the '{view.DisplayName}' view.")
            .Accepts<QueryApiRequest>(
                "application/json")
            .Produces<QueryResult<TView>>()
            .Produces<KaleidoErrorResponse>(400);
    }

    private static void MapTypedDirectQueryEndpoint<TQueryContext>(
        IEndpointRouteBuilder endpoints,
        string route,
        QueryableContextRegistryItem context,
        KaleidoHttpOptions options)
        where TQueryContext : class
    {
        var fields = context.Fields;

        endpoints.MapPost(
                route,
                async (
                    QueryApiRequest<EmptyQueryViewParameters> request,
                    IQueryableService queryable,
                    CancellationToken cancellationToken) =>
                    Results.Ok(
                        await queryable.QueryAsync<TQueryContext, TQueryContext>(
                            new QueryRequest<EmptyQueryViewParameters>(
                                Query: request.Query.ToQueryBody(fields),
                                ViewParameters: request.Parameters),
                            cancellationToken)))
            .WithKaleidoAuthorization(context.Authorization, options)
            .WithName(
                QueryableEndpointNames.QueryContextEndpointName(
                    context.Name.ToLowerInvariant()))
            .WithTags(
                context.DisplayName ?? context.Name)
            .WithSummary(
                $"Query {context.DisplayName ?? context.Name}.")
            .WithDescription(
                $"Executes a query directly against the '{context.DisplayName ?? context.Name}' query context.")
            .Accepts<QueryApiRequest>(
                "application/json")
            .Produces<QueryResult<TQueryContext>>()
            .Produces<KaleidoErrorResponse>(400);
    }

}

using System.Reflection;
using Kaleido.Queryable.Records;
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
    public static IEndpointRouteBuilder MapQueryable(
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
                "This service has no query contexts. Remove the MapQueryable() call.");
        }

        var contextRegistry =
            endpoints.ServiceProvider
                .GetRequiredService<IQueryContextRegistry>();

        var viewRegistry =
            endpoints.ServiceProvider
                .GetRequiredService<IQueryViewRegistry>();

        var delegatedViewRegistry =
            endpoints.ServiceProvider
                .GetRequiredService<IDelegatedQueryViewRegistry>();

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
                QueryableContractUrls.QueryablePrefix(serviceName));

        logger.LogInformation(
            "Queryable mapped at route prefix {RoutePrefix} with {QueryContextCount} query contexts, {QueryViewCount} query views, and {DelegatedQueryViewCount} delegated query views.",
            QueryableContractUrls.QueryablePrefix(serviceName),
            contextRegistry.Registrations.Count,
            viewRegistry.Registrations.Count,
            delegatedViewRegistry.Registrations.Count);

        group.MapGet(
                "",
                () => Results.Ok(
                    queryableRegistry.Registrations
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
            () => Results.Ok(
                queryableRegistry.Registrations
                    .Select(r =>
                        QueryableRecordResponse.FromRegistryItem(
                            r,
                            serviceName))
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)))
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

        foreach (var context in contextRegistry.Registrations)
        {
            var views = viewRegistry.Registrations
                .Where(x => x.QueryContextType == context.ContextType).ToArray();

            group.MapMetadataEndpoint(
                queryableRegistry.GetRegistration(context.Metadata.Name),
                QueryableRoutePaths.QueryContextMetadata(
                    context.Metadata.Name.ToLowerInvariant()),
                serviceName);

            if (context.Metadata.Kind == QueryContextKind.Direct)
            {
                group.MapDirectQueryContext(
                    context);
            }
        }

        foreach (var delegatedContext in delegatedViewRegistry.Registrations
                     .GroupBy(x => x.QueryMetadata.Name, StringComparer.OrdinalIgnoreCase))
        {
            var metadata = delegatedContext.First().QueryMetadata;

            group.MapMetadataEndpoint(
                queryableRegistry.GetRegistration(metadata.Name),
                QueryableRoutePaths.QueryContextMetadata(
                    metadata.Name.ToLowerInvariant()),
                serviceName);
        }

        foreach (var view in viewRegistry.Registrations)
        {
            group.MapQueryView(
                contextRegistry,
                view,
                serviceName);
        }

        foreach (var view in delegatedViewRegistry.Registrations)
        {
            group.MapDelegatedQueryView(
                view,
                serviceName);
        }

        return endpoints;
    }

    public static void MapQueryView(
      this IEndpointRouteBuilder endpoints,
      IQueryContextRegistry contextRegistry,
      QueryViewRegistration view,
      string serviceName)
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
            QueryableRoutePaths.QueryViewQuery(contextName, viewName));
    }

    private static void MapDirectQueryContext(
        this IEndpointRouteBuilder endpoints,
        QueryContextRegistration context)
    {
        var method = typeof(QueryableEndpointRouteBuilderExtensions)
            .GetMethod(
                nameof(MapTypedDirectQueryEndpoint),
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                $"Method '{nameof(MapTypedDirectQueryEndpoint)}' not found.");

        method
            .MakeGenericMethod(
                context.ContextType)
            .Invoke(
                null,
                new object[]
                {
                    endpoints,
                    QueryableRoutePaths.QueryContextQuery(
                        context.Metadata.Name.ToLowerInvariant()),
                    context
                });
    }

    public static void MapDelegatedQueryView(
      this IEndpointRouteBuilder endpoints,
      DelegatedQueryViewRegistration view,
      string serviceName)
    {
        var contextName =
            view.QueryMetadata.Name.ToLowerInvariant();

        var viewName =
            view.ViewMetadata.Name.ToLowerInvariant();

        endpoints.MapDelegatedQueryEndpoint(
            view,
            QueryableRoutePaths.QueryViewQuery(contextName, viewName));
    }

    private static void MapMetadataEndpoint(
        this IEndpointRouteBuilder endpoints,
        QueryableContextRegistryItem context,
        string route,
        string serviceName)
    {
        endpoints.MapGet(
                route,
                () => Results.Ok(
                    QueryableRecordResponse.FromRegistryItem(
                        context,
                        serviceName)))
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
        QueryContextRegistration context,
        QueryViewRegistration view,
        string route)
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
                new object[]
                {
                    endpoints,
                    route,
                    context,
                    view
                });
    }

    private static void MapDelegatedQueryEndpoint(
        this IEndpointRouteBuilder endpoints,
        DelegatedQueryViewRegistration view,
        string route)
    {
        var method = typeof(QueryableEndpointRouteBuilderExtensions)
            .GetMethod(
                nameof(MapTypedDelegatedQueryEndpoint),
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                $"Method '{nameof(MapTypedDelegatedQueryEndpoint)}' not found.");

        method
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
                    [FromServices] QueryableValueNormalizer normalizer,
                    CancellationToken cancellationToken) =>
                    await GuardQueryAsync(() =>
                        queryable.QueryAsync<TQueryView, TView>(
                            new QueryRequest<TViewParameters>(
                                Query:
                                    normalizer.Normalize(
                                        request.Query,
                                        context.Metadata),
                                ViewParameters: request.Parameters),
                            cancellationToken)))
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
            .Produces<KaleidoErrorResponse>(400);
    }

    private static void MapTypedDelegatedQueryEndpoint<TQueryView, TView, TViewParameters>(
        IEndpointRouteBuilder endpoints,
        string route,
        DelegatedQueryViewRegistration view)
        where TQueryView : class
        where TView : class
        where TViewParameters : class
    {
        endpoints.MapPost(
                route,
                async (
                    QueryApiRequest<TViewParameters> request,
                    IQueryableService queryable,
                    [FromServices] QueryableValueNormalizer normalizer,
                    CancellationToken cancellationToken) =>
                    await GuardQueryAsync(() =>
                        queryable.QueryAsync<TQueryView, TView>(
                            new QueryRequest<TViewParameters>(
                                Query:
                                    normalizer.Normalize(
                                        request.Query,
                                        view.QueryMetadata),
                                ViewParameters: request.Parameters),
                            cancellationToken)))
            .WithName(
                QueryableEndpointNames.QueryViewEndpointName(
                    view.QueryMetadata.Name.ToLowerInvariant(),
                    view.ViewMetadata.Name.ToLowerInvariant()))
            .WithTags(
                $"{view.QueryMetadata.DisplayName} - {view.ViewMetadata.DisplayName}")
            .WithSummary(
                $"Query {view.ViewMetadata.DisplayName}.")
            .WithDescription(
                $"Executes a query against the '{view.ViewMetadata.DisplayName}' view.")
            .Accepts<QueryApiRequest>(
                "application/json")
            .Produces<QueryResult<TView>>()
            .Produces<KaleidoErrorResponse>(400);
    }

    private static void MapTypedDirectQueryEndpoint<TQueryContext>(
        IEndpointRouteBuilder endpoints,
        string route,
        QueryContextRegistration context)
        where TQueryContext : class
    {
        endpoints.MapPost(
                route,
                async (
                    QueryApiRequest<EmptyQueryViewParameters> request,
                    IQueryableService queryable,
                    [FromServices] QueryableValueNormalizer normalizer,
                    CancellationToken cancellationToken) =>
                    await GuardQueryAsync(() =>
                        queryable.QueryAsync<TQueryContext, TQueryContext>(
                            new QueryRequest<EmptyQueryViewParameters>(
                                Query:
                                    normalizer.Normalize(
                                        request.Query,
                                        context.Metadata),
                                ViewParameters: request.Parameters),
                            cancellationToken)))
            .WithName(
                QueryableEndpointNames.QueryContextEndpointName(
                    context.Metadata.Name.ToLowerInvariant()))
            .WithTags(
                context.Metadata.DisplayName)
            .WithSummary(
                $"Query {context.Metadata.DisplayName}.")
            .WithDescription(
                $"Executes a query directly against the '{context.Metadata.DisplayName}' query context.")
            .Accepts<QueryApiRequest>(
                "application/json")
            .Produces<QueryResult<TQueryContext>>()
            .Produces<KaleidoErrorResponse>(400);
    }

    private static async Task<IResult> GuardQueryAsync<TView>(
        Func<Task<QueryResult<TView>>> execute)
        where TView : class
    {
        try
        {
            return Results.Ok(await execute());
        }
        catch (KaleidoValidationException ex)
        {
            return ValidationErrorResult(ex);
        }
    }

    private static IResult ValidationErrorResult(KaleidoValidationException ex) =>
        Results.BadRequest(
            new KaleidoErrorResponse(
            [
                new KaleidoError(ex.Code, ex.Message)
            ]));
}

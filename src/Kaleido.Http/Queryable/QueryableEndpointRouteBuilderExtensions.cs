using System.Reflection;
using Kaleido.Http.Authorization;
using Kaleido.Queryable.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
                QueryableErrorCodes.InvalidRegistration,
                "Cannot map Queryable endpoints: Queryable runtime is not registered. " +
                "Use MapKaleidoHttp() to map Kaleido endpoints.");
        }

        var serviceOptions =
            endpoints.ServiceProvider
                .GetRequiredService<KaleidoServiceOptions>();

        var serviceName = serviceOptions.ServiceName;

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

        foreach (var context in queryableRegistry.Registrations)
        {
            if (context.Kind == QueryContextKind.Direct
                && (serviceOptions.AuthorizationMode != KaleidoAuthorizationMode.ZeroTrust
                    || context.Authorization.IsExplicit()))
            {
                group.MapDirectQueryContext(context, serviceOptions);
            }

            foreach (var view in context.Views)
            {
                if (serviceOptions.AuthorizationMode != KaleidoAuthorizationMode.ZeroTrust
                    || view.Authorization.IsExplicit())
                {
                    group.MapQueryView(context, view, serviceOptions);
                }
            }
        }

        return group;
    }

    private static void MapQueryView(
        this IEndpointRouteBuilder endpoints,
        QueryableContextRegistryItem context,
        QueryableViewRegistryItem view,
        KaleidoServiceOptions options)
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
        KaleidoServiceOptions options)
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

    private static void MapQueryEndpoint(
        this IEndpointRouteBuilder endpoints,
        QueryableContextRegistryItem context,
        QueryableViewRegistryItem view,
        string route,
        KaleidoServiceOptions options)
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
        KaleidoServiceOptions options)
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
        KaleidoServiceOptions options)
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

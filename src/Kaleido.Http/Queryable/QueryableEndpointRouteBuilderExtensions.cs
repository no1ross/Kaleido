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
    /// <remarks>
    /// Every query source gets <c>{source}/query</c>; every local view gets
    /// <c>{source}/{view}/query</c>. All sources are mapped the same way — how a source fulfils
    /// the query is not visible to the caller.
    /// </remarks>
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

        var viewCount = queryableRegistry.Registrations.Sum(s => s.Views.Count);

        logger.LogInformation(
            "Queryable mapped at route prefix {RoutePrefix} with {QuerySourceCount} query sources and {QueryViewCount} views.",
            QueryableContractUrls.QueryablePrefix(serviceName),
            queryableRegistry.Registrations.Count,
            viewCount);

        foreach (var source in queryableRegistry.Registrations)
        {
            if (serviceOptions.AuthorizationMode != KaleidoAuthorizationMode.ZeroTrust
                || source.Authorization.IsExplicit())
            {
                group.MapQuerySource(source, serviceOptions);
            }

            foreach (var view in source.Views)
            {
                if (serviceOptions.AuthorizationMode != KaleidoAuthorizationMode.ZeroTrust
                    || view.Authorization.IsExplicit())
                {
                    group.MapQueryView(source, view, serviceOptions);
                }
            }
        }

        return group;
    }

    private static void MapQueryView(
        this IEndpointRouteBuilder endpoints,
        QueryableSourceRegistryItem source,
        QueryableViewRegistryItem view,
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
                [
                    endpoints,
                    QueryableRoutePaths.QueryViewQuery(
                        source.Name.ToLowerInvariant(),
                        view.Name.ToLowerInvariant()),
                    source,
                    view,
                    options
                ]);
    }

    private static void MapQuerySource(
        this IEndpointRouteBuilder endpoints,
        QueryableSourceRegistryItem source,
        KaleidoServiceOptions options)
    {
        var method = typeof(QueryableEndpointRouteBuilderExtensions)
            .GetMethod(
                nameof(MapTypedSourceEndpoint),
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.ReflectionError,
                $"Method '{nameof(MapTypedSourceEndpoint)}' not found.");

        method
            .MakeGenericMethod(
                source.SourceType,
                source.ResultType,
                source.ParametersType)
            .Invoke(
                null,
                [
                    endpoints,
                    QueryableRoutePaths.QuerySourceQuery(source.Name.ToLowerInvariant()),
                    source,
                    options
                ]);
    }

    private static void MapTypedQueryEndpoint<TQueryView, TView, TViewParameters>(
        IEndpointRouteBuilder endpoints,
        string route,
        QueryableSourceRegistryItem source,
        QueryableViewRegistryItem view,
        KaleidoServiceOptions options)
        where TQueryView : class
        where TView : class
        where TViewParameters : class
    {
        var fields = source.Fields;

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
                    source.Name.ToLowerInvariant(),
                    view.Name.ToLowerInvariant()))
            .WithTags(
                $"{source.DisplayName} - {view.DisplayName}")
            .WithSummary(
                $"Query {view.DisplayName}.")
            .WithDescription(
                $"Executes a query against the '{view.DisplayName}' view.")
            .Accepts<QueryApiRequest>(
                "application/json")
            .Produces<QueryResult<TView>>()
            .Produces<KaleidoErrorResponse>(400);
    }

    private static void MapTypedSourceEndpoint<TSource, TResult, TParameters>(
        IEndpointRouteBuilder endpoints,
        string route,
        QueryableSourceRegistryItem source,
        KaleidoServiceOptions options)
        where TSource : class
        where TResult : class
        where TParameters : class
    {
        var fields = source.Fields;

        endpoints.MapPost(
                route,
                async (
                    QueryApiRequest<TParameters> request,
                    IQueryableService queryable,
                    CancellationToken cancellationToken) =>
                    Results.Ok(
                        await queryable.QueryAsync<TSource, TResult>(
                            new QueryRequest<TParameters>(
                                Query: request.Query.ToQueryBody(fields),
                                ViewParameters: request.Parameters),
                            cancellationToken)))
            .WithKaleidoAuthorization(source.Authorization, options)
            .WithName(
                QueryableEndpointNames.QuerySourceEndpointName(
                    source.Name.ToLowerInvariant()))
            .WithTags(
                source.DisplayName ?? source.Name)
            .WithSummary(
                $"Query {source.DisplayName ?? source.Name}.")
            .WithDescription(
                $"Executes a query against the '{source.DisplayName ?? source.Name}' query source.")
            .Accepts<QueryApiRequest>(
                "application/json")
            .Produces<QueryResult<TResult>>()
            .Produces<KaleidoErrorResponse>(400);
    }

}

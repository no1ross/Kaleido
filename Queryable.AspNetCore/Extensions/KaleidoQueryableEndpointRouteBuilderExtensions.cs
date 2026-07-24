using Kaleido.Queryable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// Provides endpoint registration extensions for Kaleido Queryable.
/// </summary>
public static class KaleidoQueryableEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the default Kaleido Queryable endpoint surface.
    /// </summary>
    /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
    /// <param name="configure">Optional endpoint configuration.</param>
    /// <returns>The same route builder instance.</returns>
    public static IEndpointRouteBuilder MapKaleidoQueryableEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<KaleidoQueryableEndpointOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = new KaleidoQueryableEndpointOptions();
        configure?.Invoke(options);

        var group = endpoints.MapGroup(options.RoutePrefix);

        group.MapGet(options.QueriesRoute, GetQueries)
            .WithName("KaleidoQueryable_GetQueries")
            .WithTags("Kaleido Queryable");

        group.MapPost(options.QueryRoute, ExecuteQuery)
            .WithName("KaleidoQueryable_ExecuteQuery")
            .WithTags("Kaleido Queryable");

        return endpoints;
    }

    private static IResult GetQueries(IKaleidoQueryableRegistry registry)
    {
        var contracts = registry
            .GetRegistrations()
            .Select(QueryableRegistrationContract.FromRegistration)
            .ToArray();

        return Results.Ok(contracts);
    }

    private static async Task<IResult> ExecuteQuery(
        string key,
        KaleidoQueryRequest request,
        IKaleidoQueryableHttpExecutor executor,
        CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(key, request, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(result);
    }
}

using Kaleido.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.AspNetCore;

public static class QueryableAspNetCoreServiceCollectionExtensions
{
    public static IQueryableBuilder AddQueryableAspNetCore(this IQueryableBuilder builder,
        Action<QueryableRouteOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IQueryableService)))
        {
            throw new InvalidOperationException("AddQueryable must be called before AddQueryableAspNetCore.");
        }

        var routeOptions = new QueryableRouteOptions(); 
        configure?.Invoke(routeOptions); 
        builder.Services.AddSingleton(routeOptions);

        builder.Services.AddRouting();

        return builder;
    }
}

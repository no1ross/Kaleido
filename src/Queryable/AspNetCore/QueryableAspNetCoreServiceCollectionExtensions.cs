using Kaleido.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.AspNetCore;

public static class QueryableAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core queryable infrastructure, reading <c>Kaleido:RoutePrefix</c>
    /// from the configuration supplied to <see cref="KaleidoServiceCollectionExtensions.AddKaleido"/>.
    /// </summary>
    public static IQueryableBuilder AddQueryableAspNetCore(this IQueryableBuilder builder)
        => builder.AddQueryableAspNetCore(o =>
        {
            var prefix = builder.Configuration[
                $"{KaleidoOptions.SectionName}:{nameof(QueryableRouteOptions.RoutePrefix)}"];
            if (!string.IsNullOrWhiteSpace(prefix))
                o.RoutePrefix = prefix;
        });

    public static IQueryableBuilder AddQueryableAspNetCore(this IQueryableBuilder builder,
        Action<QueryableRouteOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IQueryableService)))
        {
            throw new InvalidOperationException("AddQueryable must be called before AddQueryableAspNetCore.");
        }

        var routeOptions = new QueryableRouteOptions();
        configure(routeOptions);
        builder.Services.AddSingleton(routeOptions);

        builder.Services.AddRouting();

        return builder;
    }
}

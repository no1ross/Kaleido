using Kaleido.Json;
using Kaleido.Queryable.AspNetCore.OpenApi;
using Kaleido.Queryable.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.AspNetCore;

public static class QueryableAspNetCoreServiceCollectionExtensions
{
    public static IKaleidoBuilder AddQueryableAspNetCore(this IKaleidoBuilder builder,
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
        //builder.Services.AddSingleton<AspNetCoreOpenApiDocumentContributor>();

        builder.Services.AddTransient<KaleidoDocumentFilter>();

        builder.Services.AddSwaggerGen(options =>
        {
            options.DocumentFilter<KaleidoDocumentFilter>();
        });

        //// The query model uses enums such as FilterOperator, LogicalOperator, MatchMode, and SortDirection.
        //// For HTTP contracts, strings are friendlier and less brittle than numeric enum values.
        //builder.Services.Configure<JsonOptions>(options =>
        //{
        //    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        //});

        return builder;
    }
}

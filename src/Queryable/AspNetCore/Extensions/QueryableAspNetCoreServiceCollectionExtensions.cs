using Kaleido.Queryable.Records;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

namespace Kaleido.Queryable.AspNetCore;

public static class QueryableAspNetCoreServiceCollectionExtensions
{
    public static IKaleidoBuilder AddQueryableAspNetCore(this IKaleidoBuilder builder,
        Action<QueryableAspNetCoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IQueryableCatalog)))
        {
            throw new InvalidOperationException("AddQueryable must be called before AddQueryableAspNetCore.");
        }

        if (configure is not null) 
        {
            builder.Services.Configure(configure); 
        }

        //// The query model uses enums such as FilterOperator, LogicalOperator, MatchMode, and SortDirection.
        //// For HTTP contracts, strings are friendlier and less brittle than numeric enum values.
        //builder.Services.Configure<JsonOptions>(options =>
        //{
        //    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        //});

        return builder;
    }
}

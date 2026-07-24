using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// Provides service-registration extensions for Kaleido Queryable ASP.NET Core integration.
/// </summary>
public static class KaleidoQueryableAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the services required to expose Kaleido Queryable through ASP.NET Core endpoints.
    /// </summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder instance so calls can be chained.</returns>
    public static IKaleidoBuilder AddQueryableAspNetCore(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IKaleidoQueryableHttpExecutor, KaleidoQueryableHttpExecutor>();

        // The query model uses enums such as FilterOperator, LogicalOperator, MatchMode, and SortDirection.
        // For HTTP contracts, strings are friendlier and less brittle than numeric enum values.
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        return builder;
    }
}

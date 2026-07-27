using Kaleido.Json;
using Kaleido.Queryable.Records;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace Kaleido.Queryable.OpenApi;

public static class QueryableOpenApiCoreServiceCollectionExtensions
{
    public static IKaleidoBuilder AddQueryableOpenApi(this IKaleidoBuilder builder,
        Action<QueryableOpenApiOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IQueryableService)))
        {
            throw new InvalidOperationException("AddQueryable must be called before AddQueryableAspNetCore.");
        }



        return builder;
    }
}

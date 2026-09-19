using Kaleido.Exceptions;
using Kaleido.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.AspNetCore;

public static class QueryableAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core queryable infrastructure.
    /// Routes are derived from <see cref="KaleidoServiceOptions.ServiceName"/>
    /// (e.g. <c>"intake"</c> → <c>/intake/queryable/…</c>).
    /// </summary>
    public static IQueryableBuilder AddQueryableAspNetCore(this IQueryableBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IQueryableService)))
            throw new KaleidoConfigurationException("AddQueryable must be called before AddQueryableAspNetCore.");

        builder.Services.AddRouting();

        return builder;
    }
}

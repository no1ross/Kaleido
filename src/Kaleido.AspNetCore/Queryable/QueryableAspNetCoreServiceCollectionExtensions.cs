using Kaleido.Exceptions;
using Kaleido.Json;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.AspNetCore;

public static class QueryableAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core queryable infrastructure.
    /// Routes are derived from <see cref="KaleidoServiceOptions.ServiceName"/>
    /// (e.g. <c>"intake"</c> → <c>/intake/queryable/…</c>).
    /// </summary>
    internal static IKaleidoBuilder AddQueryableAspNetCore(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IQueryableRegistry)))
        {
            // Queryable runtime not registered (Process-only service) - this is valid
            return builder;
        }

        builder.Services.AddRouting();

        return builder;
    }
}

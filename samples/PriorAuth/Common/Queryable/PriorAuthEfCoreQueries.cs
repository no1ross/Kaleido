using Kaleido.Queryable.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Queryable;

/// <summary>EF Core query execution for the PriorAuth services whose query sources read an EF Core database.</summary>
public static class PriorAuthEfCoreQueries
{
    /// <summary>
    /// Registers <see cref="EfCoreQueryContextExecutor{TView}"/> for every query source of the service, so
    /// a full page is counted with one <c>SELECT COUNT(*)</c> (#352). Kaleido registers its default
    /// executor with <c>TryAdd</c>, so this one wins.
    /// </summary>
    public static IKaleidoBuilder AddPriorAuthEfCoreQueries(this IKaleidoBuilder builder)
    {
        builder.Services.AddSingleton(typeof(IQueryContextExecutor<>), typeof(EfCoreQueryContextExecutor<>));

        return builder;
    }
}

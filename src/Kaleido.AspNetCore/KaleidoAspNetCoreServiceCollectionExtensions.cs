using Kaleido.Process.AspNetCore;
using Kaleido.Queryable.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.AspNetCore;

public static class KaleidoAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core infrastructure for Kaleido Process and Queryable subsystems.
    /// This includes routing, HttpContextAccessor, and ASP.NET Core-specific services.
    /// </summary>
    public static IKaleidoBuilder AddAspNetCore(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddProcessorAspNetCore();
        builder.AddQueryableAspNetCore();

        return builder;
    }
}

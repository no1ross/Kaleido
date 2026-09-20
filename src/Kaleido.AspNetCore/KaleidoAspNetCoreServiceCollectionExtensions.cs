using Kaleido.AspNetCore.Process;
using Kaleido.AspNetCore.Queryable;
using Kaleido.AspNetCore.Startup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.AspNetCore;

public static class KaleidoAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers ASP.NET Core infrastructure for Kaleido Process and Queryable subsystems.
    /// This includes routing, HttpContextAccessor, ASP.NET Core-specific services,
    /// and automatic middleware registration for correlation context and exception handling.
    /// </summary>
    public static IKaleidoBuilder AddAspNetCore(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddProcessorAspNetCore();
        builder.AddQueryableAspNetCore();

        // Auto-register middleware via IStartupFilter
        builder.Services.AddSingleton<IStartupFilter, KaleidoStartupFilter>();

        return builder;
    }
}

using Kaleido.AspNetCore.Startup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.AspNetCore;

internal static class KaleidoAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers shared ASP.NET Core infrastructure for Kaleido.
    /// This is an internal implementation detail — consumers should call <c>AddHttp()</c>
    /// (or a future <c>AddGrpc()</c>) instead of this method directly.
    /// </summary>
    internal static IKaleidoBuilder AddAspNetCore(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();

        // Auto-register middleware via IStartupFilter
        builder.Services.AddSingleton<IStartupFilter, KaleidoStartupFilter>();

        return builder;
    }
}

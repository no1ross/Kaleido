using Kaleido.AspNetCore;
using Kaleido.Http.Process.Services;
using Kaleido.Process.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http;

public static class KaleidoHttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers HTTP transport infrastructure for Kaleido.
    /// This includes ASP.NET Core routing, middleware, correlation context, and HTTP-specific
    /// execution services. Call this in place of <c>AddAspNetCore()</c>.
    /// </summary>
    /// <remarks>
    /// When a gRPC transport is available, use <c>AddGrpc()</c> instead.
    /// Both <c>AddHttp()</c> and future transport extensions call <c>AddAspNetCore()</c> internally —
    /// consumers should never call <c>AddAspNetCore()</c> directly.
    /// </remarks>
    public static IKaleidoBuilder AddHttp(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Wire up shared ASP.NET Core DI infrastructure (routing, HttpContextAccessor, middleware)
        builder.AddAspNetCore();

        // Register HTTP-specific execution services — only when Process runtime is present
        if (builder.Services.Any(d => d.ServiceType == typeof(IProcessRegistry)))
        {
            builder.Services.TryAddScoped<IProcessExecutionService, ProcessExecutionService>();
            builder.Services.TryAddScoped<IProcessStateService, ProcessStateService>();
        }

        return builder;
    }
}

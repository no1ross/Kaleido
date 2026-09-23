using Kaleido.Http.Startup;
using Kaleido.Process.Registry;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http;

public static class KaleidoHttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers HTTP transport infrastructure for Kaleido: ASP.NET Core routing,
    /// <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/>, correlation and exception
    /// middleware, and HTTP-specific execution services.
    /// </summary>
    public static IKaleidoBuilder AddHttp(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Generic ASP.NET Core infrastructure needed by HTTP transport
        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();

        // Register Kaleido middleware pipeline via startup filter
        builder.Services.AddSingleton<IStartupFilter, KaleidoStartupFilter>();

        // Register HTTP-specific execution services — only when Process runtime is present
        if (builder.Services.Any(d => d.ServiceType == typeof(IProcessRegistry)))
        {
            builder.Services.TryAddScoped<IProcessExecutionService, ProcessExecutionService>();
            builder.Services.TryAddScoped<IProcessStateService, ProcessStateService>();
        }

        builder.Services.TryAddSingleton<QueryableValueNormalizer>();
        builder.Services.TryAddSingleton<IProcessResponseFactory, ProcessResponseFactory>();
        builder.Services.TryAddSingleton<IProcessExecutionResponseFactory, ProcessExecutionResponseFactory>();

        return builder;
    }
}

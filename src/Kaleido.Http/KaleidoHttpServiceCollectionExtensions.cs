using Kaleido.Http.Authorization;
using Kaleido.Http.Startup;
using Kaleido.Process.Registry;
using Microsoft.AspNetCore.Authorization;
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
    public static IKaleidoBuilder AddHttp(
        this IKaleidoBuilder builder,
        Action<KaleidoHttpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Generic ASP.NET Core infrastructure needed by HTTP transport
        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();

        // Transport options — always registered so map-time conventions and
        // runtime services can resolve it unconditionally.
        var httpOptions = new KaleidoHttpOptions();
        configure?.Invoke(httpOptions);
        builder.Services.AddSingleton(httpOptions);

        // Capability authorization: runtime evaluator + KaleidoErrorResponse
        // bodies for 401/403 authorization results.
        builder.Services.TryAddScoped<IKaleidoAuthorizer, KaleidoAuthorizer>();
        builder.Services.Replace(
            ServiceDescriptor.Singleton<
                IAuthorizationMiddlewareResultHandler,
                KaleidoAuthorizationResultHandler>());

        // Register Kaleido middleware pipeline via startup filter
        builder.Services.AddSingleton<IStartupFilter, KaleidoStartupFilter>();

        // Register HTTP-specific execution services — only when Process runtime is present
        if (builder.Services.Any(d => d.ServiceType == typeof(IProcessRegistry)))
        {
            builder.Services.TryAddScoped<IProcessExecutionService, ProcessExecutionService>();
            builder.Services.TryAddScoped<IProcessStateService, ProcessStateService>();
        }

        builder.Services.TryAddSingleton<Registry.IRegistrySnapshotStore, Registry.InMemoryRegistrySnapshotStore>();
        builder.Services.TryAddSingleton<IProcessResponseFactory, ProcessResponseFactory>();
        builder.Services.TryAddSingleton<IProcessExecutionResponseFactory, ProcessExecutionResponseFactory>();

        return builder;
    }
}

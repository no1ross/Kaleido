using Kaleido.Http.Authorization;
using Kaleido.Http.Registry;
using Kaleido.Processor.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http;

/// <summary>
/// Registers the Kaleido HTTP transport (routing, middleware pipeline, capability authorization,
/// HTTP execution services and the unified registry) on an <see cref="IKaleidoBuilder"/>; the entry
/// point is <see cref="AddHttp"/>. Map the endpoints with <c>MapKaleidoHttp()</c>.
/// </summary>
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

        // A second call would silently replace the first one's options (#301).
        if (builder.Services.Any(x => x.ServiceType == typeof(KaleidoHttpOptions)))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.DuplicateRegistration,
                "AddHttp() was called more than once on this Kaleido builder. Configure the HTTP transport in a single AddHttp() call.");
        }

        // Generic ASP.NET Core infrastructure needed by HTTP transport
        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();

        // Transport options — always registered so map-time conventions and
        // runtime services can resolve it unconditionally.
        var httpOptions = new KaleidoHttpOptions();
        configure?.Invoke(httpOptions);
        builder.Services.AddSingleton(httpOptions);

        // Capability authorization: runtime evaluator. 401/403 responses are
        // produced by the host's authentication scheme (standard ASP.NET Core).
        builder.Services.TryAddScoped<IKaleidoAuthorizer, KaleidoAuthorizer>();
        builder.Services.TryAddSingleton<Middleware.IKaleidoExceptionHandler, Middleware.KaleidoExceptionHandler>();

        // Register HTTP-specific execution services — only when Process runtime is present
        if (builder.Services.Any(d => d.ServiceType == typeof(IProcessorRegistry)))
        {
            builder.Services.TryAddScoped<IProcessExecutionService, ProcessExecutionService>();
            builder.Services.TryAddScoped<IProcessStateService, ProcessStateService>();
        }

        builder.Services.TryAddSingleton<Registry.IRegistrySnapshotStore, Registry.InMemoryRegistrySnapshotStore>();
        builder.Services.TryAddSingleton<IProcessorResponseFactory, ProcessorResponseFactory>();
        builder.Services.TryAddSingleton<IProcessExecutionResponseFactory, ProcessExecutionResponseFactory>();

        // The unified registry, in process: the registry endpoint and in-host consumers (e.g. the
        // MCP adapter) both use IKaleidoRegistryService. Downstream registries come from
        // AddHttpClients(), which replaces the "no downstream" default when clients are configured.
        builder.Services.TryAddSingleton<RegistrySnapshot>();
        builder.Services.TryAddSingleton<LocalRegistrations>();
        builder.Services.TryAddScoped<IKaleidoDownstreamRegistries, NoDownstreamRegistries>();
        builder.Services.TryAddScoped<IKaleidoRegistryService, KaleidoRegistryService>();

        // The same registry unfiltered, for in-host consumers that must not depend on one caller's
        // view (the MCP adapter's stable tool names, #435); never served to a caller.
        builder.Services.TryAddScoped<IKaleidoUnfilteredRegistry, KaleidoRegistryService>();

        return builder;
    }
}

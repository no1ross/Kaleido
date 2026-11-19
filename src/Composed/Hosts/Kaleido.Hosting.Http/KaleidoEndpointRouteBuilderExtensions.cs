using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Hosting.Http;

/// <summary>Explicit HTTP endpoint composition over the capability and central Registry owners.</summary>
public static partial class KaleidoEndpointRouteBuilderExtensions
{
    /// <summary>Creates a root for explicitly selected Kaleido HTTP endpoints; maps none by itself.</summary>
    /// <param name="endpoints">The host's route builder.</param>
    /// <returns>A builder that shares host conventions across selected endpoint groups.</returns>
    public static IKaleidoHttpEndpointBuilder MapKaleido(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return new KaleidoHttpEndpointBuilder(endpoints.MapGroup(string.Empty));
    }

    /// <summary>Maps the local Process execute, step and state endpoints through its HTTP adapter.</summary>
    /// <param name="builder">The Kaleido HTTP composition root.</param>
    /// <returns>The same root, ready for another explicit mapping or host conventions.</returns>
    public static IKaleidoHttpEndpointBuilder MapProcess(this IKaleidoHttpEndpointBuilder builder)
    {
        var root = RequireRoot(builder);
        root.RequireAvailable("Process");
        RequireLocalRuntime<IProcessorRuntime>(root.Endpoints, ProcessorErrorCodes.InvalidRegistration, "AddProcess()");
        RequireLocalRuntime<KaleidoHttpServerOptions>(root.Endpoints, ConfigurationErrorCodes.InvalidOptions, "AddHttp()");
        RequireLocalRuntime<KaleidoProcessHttpOptions>(root.Endpoints, ConfigurationErrorCodes.InvalidOptions, "AddProcessHttpServer()");
        ValidateLocalAuthorization(root);
        root.Endpoints.MapProcessor();
        root.MarkMapped("Process");
        return builder;
    }

    /// <summary>Maps the local Queryable source and view endpoints through its HTTP adapter.</summary>
    /// <param name="builder">The Kaleido HTTP composition root.</param>
    /// <returns>The same root, ready for another explicit mapping or host conventions.</returns>
    public static IKaleidoHttpEndpointBuilder MapQueryable(this IKaleidoHttpEndpointBuilder builder)
    {
        var root = RequireRoot(builder);
        root.RequireAvailable("Queryable");
        RequireLocalRuntime<IQueryableService>(root.Endpoints, QueryableErrorCodes.InvalidRegistration, "AddQueryable()");
        RequireLocalRuntime<KaleidoHttpServerOptions>(root.Endpoints, ConfigurationErrorCodes.InvalidOptions, "AddHttp()");
        ValidateLocalAuthorization(root);
        root.Endpoints.MapQueryable();
        root.MarkMapped("Queryable");
        return builder;
    }

    /// <summary>Maps the central Registry authority endpoints, not a service's local catalog.</summary>
    /// <param name="builder">The Kaleido HTTP composition root.</param>
    /// <returns>The same root, ready for authority host conventions.</returns>
    public static IKaleidoHttpEndpointBuilder MapCentralRegistry(this IKaleidoHttpEndpointBuilder builder)
    {
        var root = RequireRoot(builder);
        root.RequireAvailable("CentralRegistry");
        root.Endpoints.MapKaleidoControlRegistry();
        root.MarkMapped("CentralRegistry");
        return builder;
    }

    private static KaleidoHttpEndpointBuilder RequireRoot(IKaleidoHttpEndpointBuilder? builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder as KaleidoHttpEndpointBuilder
            ?? throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidOptions,
                "Use MapKaleido() to create a Kaleido HTTP endpoint composition root.");
    }

    private static void RequireLocalRuntime<TService>(IEndpointRouteBuilder endpoints, string code, string registration)
    {
        if (endpoints.ServiceProvider.GetService<IServiceProviderIsService>()?.IsService(typeof(TService)) != true)
        {
            throw new KaleidoConfigurationException(code, $"Call {registration} before mapping its HTTP endpoints.");
        }
    }

    private static void ValidateLocalAuthorization(KaleidoHttpEndpointBuilder root)
    {
        if (root.LocalAuthorizationChecked)
        {
            return;
        }

        var services = root.Endpoints.ServiceProvider;
        var server = services.GetService<KaleidoServerOptions>()
            ?? throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidOptions,
                "Call AddServer() before mapping local Kaleido HTTP endpoints.");
        if (server.AuthorizationMode == KaleidoAuthorizationMode.None)
        {
            LogAuthorizationDisabled(services.GetRequiredService<ILoggerFactory>().CreateLogger("Kaleido.Hosting.Http"));
        }
        else if (services.GetService<IAuthenticationSchemeProvider>()?.GetAllSchemesAsync().GetAwaiter().GetResult().Any() != true)
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.AuthenticationNotConfigured,
                $"AuthorizationMode is {server.AuthorizationMode} but no authentication scheme is registered. Configure AddAuthentication(...) and the host's authentication middleware.");
        }

        root.LocalAuthorizationChecked = true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AuthorizationMode is None: Kaleido capability authorization is disabled. Set AuthorizationMode to Authenticated or ZeroTrust for enforcement.")]
    private static partial void LogAuthorizationDisabled(ILogger logger);
}

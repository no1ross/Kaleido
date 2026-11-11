using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Server;

/// <summary>Registers transport-neutral authorization options for a hosted Kaleido service.</summary>
public static class KaleidoServerServiceCollectionExtensions
{
    /// <summary>Registers the effective service authorization mode and default rule.</summary>
    /// <remarks>
    /// The <c>Kaleido</c> configuration section overrides a copy of <paramref name="options"/>,
    /// preserving the existing <c>Kaleido:AuthorizationMode</c> and
    /// <c>Kaleido:DefaultAuthorization</c> keys. This method binds options; the owning transport
    /// enforces them after authentication. It registers no HTTP services.
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="options">Authorization options from code, or defaults when omitted.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">Settings are invalid or Server was registered twice.</exception>
    public static IKaleidoBuilder AddServer(this IKaleidoBuilder builder, KaleidoServerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        if (services.Any(x => x.ServiceType == typeof(KaleidoServerOptions)))
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.DuplicateRegistration, "AddServer() was called more than once on this service collection.");
        }

        var serverOptions = builder.BindOptions(options);
        serverOptions = serverOptions with
        {
            DefaultAuthorization = serverOptions.DefaultAuthorization with { Roles = [.. serverOptions.DefaultAuthorization.Roles] },
        };
        services.AddSingleton(serverOptions);

        return builder;
    }
}

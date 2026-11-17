using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Registry.Http.Client;

/// <summary>Fixed-endpoint Registry HTTP client registration using the shared gateway credential pipeline.</summary>
public static class KaleidoRegistryHttpClientServiceCollectionExtensions
{
    /// <summary>Registers Registry HTTP calls; does not start participation until <c>AddRegistryClient()</c> is called.</summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="options">Gateway client settings from code; configuration overrides them.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddRegistryHttpClient(this IKaleidoBuilder builder, KaleidoClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(x => x.ServiceType == typeof(IKaleidoRegistryClient)))
        {
            return builder;
        }

        builder.AddKaleidoHttpClientPipeline(options);
        builder.AddKaleidoNamedClient(KaleidoHttpRegistryClient.ClientName);
        builder.Services.TryAddScoped<IKaleidoRegistryClient, KaleidoHttpRegistryClient>();
        return builder;
    }

    /// <summary>Explicitly opts a service into Registry hydration and HTTP publication/heartbeat when it has callable capabilities.</summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="options">Gateway client settings from code; configuration overrides them.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddRegistryHttpParticipation(this IKaleidoBuilder builder, KaleidoClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddRegistryClient().AddRegistryHttpClient(options);
    }
}

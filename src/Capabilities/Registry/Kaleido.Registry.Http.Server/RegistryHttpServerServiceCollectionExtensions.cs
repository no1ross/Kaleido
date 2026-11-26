using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Registry.Http.Server;

/// <summary>Registers the Registry-specific ASP.NET Core read adapter beside the neutral Registry authority.</summary>
public static class RegistryHttpServerServiceCollectionExtensions
{
    /// <summary>Adds caller-filtered and unfiltered Registry reads for the HTTP listener.</summary>
    /// <param name="builder">The Kaleido builder after the authority and shared HTTP infrastructure are registered.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddRegistryHttpServer(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddScoped<KaleidoRegistryService>();
        builder.Services.TryAddScoped<IKaleidoRegistryService>(services => services.GetRequiredService<KaleidoRegistryService>());
        builder.Services.TryAddScoped<IKaleidoUnfilteredRegistry>(services => services.GetRequiredService<KaleidoRegistryService>());

        return builder;
    }
}

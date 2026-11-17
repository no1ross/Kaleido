using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Process.Http;

/// <summary>Opt-in Process HTTP operation bindings for local Registry publication.</summary>
public static class ProcessHttpRegistryBindingServiceCollectionExtensions
{
    /// <summary>Contributes HTTP execute and state routes only when this host maps Process HTTP endpoints.</summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddProcessHttpBindings(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessRegistryBindingContributor, ProcessHttpRegistryBindingContributor>());
        return builder;
    }
}

using Kaleido.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Queryable.Http;

/// <summary>Opt-in Queryable HTTP operation bindings for local Registry publication.</summary>
public static class QueryableHttpRegistryBindingServiceCollectionExtensions
{
    /// <summary>Contributes HTTP query routes only when this host maps Queryable HTTP endpoints.</summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder.</returns>
    public static IKaleidoBuilder AddQueryableHttpBindings(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IQueryableRegistryBindingContributor, QueryableHttpRegistryBindingContributor>());
        return builder;
    }
}

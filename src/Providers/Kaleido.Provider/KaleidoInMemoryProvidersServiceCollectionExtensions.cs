using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Provider;

/// <summary>Explicitly registers Kaleido's in-memory Process and Eventing store implementations.</summary>
public static class KaleidoInMemoryProvidersServiceCollectionExtensions
{
    /// <summary>Chooses in-memory process state, idempotency and event outbox stores when no other store is registered.</summary>
    /// <remarks>
    /// In-memory records are lost on restart and are not shared across service instances. Intended for
    /// local development and tests; the store implementations log a warning when created. Durable
    /// registrations already chosen by the host are left untouched.
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IKaleidoBuilder AddInMemoryProviders(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IProcessContextStore, InMemoryProcessContextStore>();
        builder.Services.TryAddSingleton<IProcessIdempotencyStore, InMemoryProcessIdempotencyStore>();
        builder.Services.TryAddSingleton<IKaleidoOutboxStore, InMemoryKaleidoOutboxStore>();
        return builder;
    }
}

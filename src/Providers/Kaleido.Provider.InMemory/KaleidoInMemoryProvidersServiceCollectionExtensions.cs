using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Provider.InMemory;

/// <summary>Explicitly registers Kaleido's in-memory Process and Eventing store implementations.</summary>
public static class KaleidoInMemoryProvidersServiceCollectionExtensions
{
    /// <summary>Explicitly selects in-memory Process state, Eventing outbox and server idempotency stores.</summary>
    /// <remarks>
    /// Memory is lost on restart and not shared by service instances. Each store warns when constructed.
    /// Choosing a different provider for any store is a conflict regardless of call order.
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">A different provider was already chosen for a store.</exception>
    public static IKaleidoBuilder AddInMemoryProviders(this IKaleidoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Require<IProcessContextStore, InMemoryProcessContextStore>(builder, "context store", ConfigurationErrorCodes.ConflictingStore);
        Require<IKaleidoOutboxStore, InMemoryKaleidoOutboxStore>(builder, "outbox store", ConfigurationErrorCodes.ConflictingOutbox);
        Require<IProcessIdempotencyStore, InMemoryProcessIdempotencyStore>(builder, "idempotency store", ConfigurationErrorCodes.ConflictingStore);
        return builder.AddInMemoryContextProvider().AddInMemoryOutboxProvider().AddInMemoryIdempotencyProvider();
    }

    /// <summary>Explicitly selects in-memory Process state when no context provider was chosen.</summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">A different Process state provider was chosen.</exception>
    public static IKaleidoBuilder AddInMemoryContextProvider(this IKaleidoBuilder builder) =>
        Register<IProcessContextStore, InMemoryProcessContextStore>(builder, "context store", ConfigurationErrorCodes.ConflictingStore);

    /// <summary>Explicitly selects the in-memory Eventing outbox when no outbox provider was chosen.</summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">A different outbox provider was chosen.</exception>
    public static IKaleidoBuilder AddInMemoryOutboxProvider(this IKaleidoBuilder builder) =>
        Register<IKaleidoOutboxStore, InMemoryKaleidoOutboxStore>(builder, "outbox store", ConfigurationErrorCodes.ConflictingOutbox);

    /// <summary>Explicitly selects in-memory Process.Server admission when no idempotency provider was chosen.</summary>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">A different idempotency provider was chosen.</exception>
    public static IKaleidoBuilder AddInMemoryIdempotencyProvider(this IKaleidoBuilder builder) =>
        Register<IProcessIdempotencyStore, InMemoryProcessIdempotencyStore>(builder, "idempotency store", ConfigurationErrorCodes.ConflictingStore);

    private static IKaleidoBuilder Register<TService, TStore>(IKaleidoBuilder builder, string what, string code)
        where TService : class
        where TStore : class, TService
    {
        ArgumentNullException.ThrowIfNull(builder);
        Require<TService, TStore>(builder, what, code);
        builder.Services.TryAddSingleton<TService, TStore>();
        return builder;
    }

    private static void Require<TService, TStore>(IKaleidoBuilder builder, string what, string code)
        where TService : class
        where TStore : class, TService
    {
        var registered = builder.Services.LastOrDefault(x => x.ServiceType == typeof(TService));
        if (registered is not null && registered.ImplementationType != typeof(TStore))
        {
            throw new KaleidoConfigurationException(code,
                $"Two {what} providers were chosen: {registered.ImplementationType?.Name ?? registered.ServiceType.Name} and {typeof(TStore).Name}.");
        }
    }
}

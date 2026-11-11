using Kaleido.Process.Server.Idempotency;

namespace Kaleido.Process.Server;

/// <summary>Explicit store registration for server-side Process admission.</summary>
public static class ProcessServerServiceCollectionExtensions
{
    /// <summary>Registers a scoped idempotency store instead of the in-memory reference implementation.</summary>
    /// <typeparam name="TStore">The idempotency store implementation.</typeparam>
    /// <param name="builder">The Kaleido builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">Another idempotency store has already been chosen.</exception>
    public static IKaleidoBuilder AddIdempotencyStore<TStore>(this IKaleidoBuilder builder)
        where TStore : class, IProcessIdempotencyStore =>
        KaleidoServiceCollectionExtensions.Replace<IProcessIdempotencyStore, TStore, InMemoryProcessIdempotencyStore>(builder, "idempotency store");
}

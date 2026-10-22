using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.Registry;

/// <summary>
/// The registries of a host's downstream services: what an aggregating host (a router or gateway)
/// merges into its own registry. <c>Kaleido.Http.Client</c> provides it for the clients registered
/// with <c>AddHttpClients()</c>; a host without clients has no downstream registries.
/// </summary>
internal interface IKaleidoDownstreamRegistries
{
    /// <summary>
    /// Fetches every downstream registry (each through its client's own cache). A client that fails
    /// is reported in <see cref="DownstreamRegistries.Errors"/>; the others are still returned.
    /// </summary>
    /// <param name="maxParallelism">At most this many fetches in flight; null = all at once.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    Task<DownstreamRegistries> GetAsync(
        int? maxParallelism,
        CancellationToken cancellationToken);

    /// <summary>
    /// Drops each downstream client's cached registry, so the next fetch re-reads it. Best effort per
    /// client: a client that cannot be invalidated is logged and the others are still invalidated.
    /// </summary>
    /// <param name="cancellationToken">Cancels the removals from the snapshot store.</param>
    /// <returns>A task that completes once every downstream snapshot has been dropped.</returns>
    Task InvalidateAsync(
        CancellationToken cancellationToken);
}

/// <summary>The downstream registries, and the clients that could not be read.</summary>
[ExcludeFromCodeCoverage]
internal sealed record DownstreamRegistries(
    IReadOnlyCollection<ProcessorRegistryResponse> Processes,
    IReadOnlyCollection<QueryableSourceResponse> Queryables,
    IReadOnlyCollection<RegistryClientError> Errors)
{
    /// <summary>No downstream services.</summary>
    public static DownstreamRegistries None { get; } = new([], [], []);
}

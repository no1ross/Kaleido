using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.Registry;

/// <summary>
/// The registries of a host's downstream services: what an aggregating host (a router or gateway)
/// merges into its own registry. <c>Kaleido.Http.Client</c> provides it for the clients registered
/// with <c>AddHttpClients()</c>; a host without clients has no downstream registries.
/// </summary>
public interface IKaleidoDownstreamRegistries
{
    /// <summary>
    /// Fetches every downstream registry (each through its client's own cache). A client that fails
    /// is reported in <see cref="DownstreamRegistries.Errors"/>; the others are still returned.
    /// </summary>
    /// <param name="maxParallelism">At most this many fetches in flight; null = all at once.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    Task<DownstreamRegistries> GetAsync(
        int? maxParallelism = null,
        CancellationToken cancellationToken = default);

    /// <summary>Drops each downstream client's cached registry, so the next fetch re-reads it.</summary>
    void Invalidate();
}

/// <summary>The downstream registries, and the clients that could not be read.</summary>
[ExcludeFromCodeCoverage]
public sealed record DownstreamRegistries(
    IReadOnlyCollection<ProcessorRegistryResponse> Processes,
    IReadOnlyCollection<QueryableSourceResponse> Queryables,
    IReadOnlyCollection<RegistryClientError> Errors)
{
    /// <summary>No downstream services.</summary>
    public static DownstreamRegistries None { get; } = new([], [], []);
}

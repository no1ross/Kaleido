using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.Registry;

/// <summary>
/// Where a host's full registry comes from (#553): every service's processors and query sources, each
/// version's status and what is missing, held in memory. One contract, filled differently: the control
/// registry from its store, a host that reads a control registry by fetching it, a host with neither from
/// nothing beyond its own registrations - and, until it is removed, the per-client fan-out of
/// <c>AddHttpClients()</c>. The host's registry service adds its own registrations, caches the result and
/// filters it per caller; lookups (#524) and hand-off targets (#198) find other services in it.
/// </summary>
internal interface IKaleidoRegistrySource
{
    /// <summary>
    /// The full registry. A source that fetches reports a failed read in
    /// <see cref="RegistrySourceContents.Errors"/> and still returns what it could read.
    /// </summary>
    /// <param name="maxParallelism">At most this many fetches in flight, for a source that fans out; null = all at once.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<RegistrySourceContents> GetAsync(
        int? maxParallelism,
        CancellationToken cancellationToken);

    /// <summary>
    /// Drops any copy the source keeps, so the next <see cref="GetAsync"/> reads afresh; nothing to do for
    /// a source that keeps none.
    /// </summary>
    /// <param name="cancellationToken">Cancels the removals.</param>
    Task RefreshAsync(
        CancellationToken cancellationToken);
}

/// <summary>A host's full registry, as its source holds it.</summary>
[ExcludeFromCodeCoverage]
internal sealed record RegistrySourceContents(
    IReadOnlyCollection<ProcessorRegistryResponse> Processes,
    IReadOnlyCollection<QueryableSourceResponse> Queryables,
    IReadOnlyCollection<RegistryClientError> Errors)
{
    /// <summary>Nothing beyond the host's own registrations.</summary>
    public static RegistrySourceContents None { get; } = new([], [], []);

    /// <summary>Each service version's status, when the source is or reads a control registry (#553).</summary>
    public IReadOnlyCollection<RegistryServiceStatus> Services { get; init; } = [];

    /// <summary>What a control registry expects and has no registration for (#553).</summary>
    public IReadOnlyCollection<RegistryMissingService> Missing { get; init; } = [];
}

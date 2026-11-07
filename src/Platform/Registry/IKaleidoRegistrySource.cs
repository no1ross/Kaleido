namespace Kaleido.Platform.Registry;

/// <summary>
/// Where a host's registry comes from (#553), in the one shape every participant shares:
/// <see cref="KaleidoRegistry"/> (each capability with its overall status) and each service version's
/// availability, <see cref="KaleidoRegistryAvailability"/>. One contract, filled
/// differently:
/// <list type="bullet">
/// <item>the control registry, from its store, and a participant, from the control registry: the full
/// registry (<see cref="IsFullRegistry"/>), which the host serves as it is;</item>
/// <item>a host with neither: nothing, the host serving its own registrations;</item>
/// <item>until it is removed, the per-client fan-out of <c>AddHttpClients()</c>, which the host merges with its
/// own registrations.</item>
/// </list>
/// The host's registry service caches the result and filters it per caller; lookups (#524) and hand-off
/// targets (#198) find other services in it.
/// </summary>
internal interface IKaleidoRegistrySource
{
    /// <summary>
    /// True when the source's registry is the whole registry, this host's own registrations included (it
    /// registered them): the host serves it as it is, without adding its own.
    /// </summary>
    bool IsFullRegistry { get; }

    /// <summary>
    /// The registry. A source that fetches reports a failed read in <see cref="KaleidoRegistry.Errors"/>
    /// and still returns what it could read.
    /// </summary>
    /// <param name="maxParallelism">At most this many fetches in flight, for a source that fans out; null = all at once.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<KaleidoRegistry> GetAsync(
        int? maxParallelism,
        CancellationToken cancellationToken);

    /// <summary>
    /// Each service version's availability (current, address, overall status); empty from a source that
    /// knows no versions.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<KaleidoRegistryAvailability> GetAvailabilityAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Drops any copy the source keeps, so the next read is afresh; nothing to do for a source that keeps none.
    /// </summary>
    /// <param name="cancellationToken">Cancels the removals.</param>
    Task RefreshAsync(
        CancellationToken cancellationToken);
}

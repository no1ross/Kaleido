namespace Kaleido.Platform.Registry;

/// <summary>
/// The full central registry (#553): from the store in the registry host, or from the hydrated
/// copy on a participant. Both expose <see cref="KaleidoRegistry"/> with capability status and
/// <see cref="KaleidoRegistryAvailability"/> for each service version. In-host readers filter
/// the full copy per caller; lookups and hand-offs use it without per-service discovery.
/// </summary>
internal interface IKaleidoRegistrySource
{
    /// <summary>
    /// The registry. A source that fetches reports a failed read in <see cref="KaleidoRegistry.Errors"/>
    /// and still returns what it could read.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<KaleidoRegistry> GetAsync(CancellationToken cancellationToken);

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

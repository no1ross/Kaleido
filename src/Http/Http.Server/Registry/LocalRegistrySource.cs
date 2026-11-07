using Kaleido.Platform.Registry;

namespace Kaleido.Http.Server.Registry;

/// <summary>
/// The default <see cref="IKaleidoRegistrySource"/>: nothing beyond the host's own registrations, for a
/// host that reads no control registry and has no clients.
/// </summary>
internal sealed class LocalRegistrySource : IKaleidoRegistrySource
{
    private static readonly KaleidoRegistry Nothing = new();
    private static readonly KaleidoRegistryAvailability NoStatus = new();

    public bool IsFullRegistry => false;

    public Task<KaleidoRegistry> GetAsync(
        int? maxParallelism,
        CancellationToken cancellationToken) =>
        Task.FromResult(Nothing);

    public Task<KaleidoRegistryAvailability> GetAvailabilityAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(NoStatus);

    // Nothing is kept, so there is nothing to drop.
    public Task RefreshAsync(
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

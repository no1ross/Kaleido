namespace Kaleido.Http.Registry;

/// <summary>
/// The default <see cref="IKaleidoRegistrySource"/>: nothing beyond the host's own registrations, for a
/// host that reads no control registry and has no clients.
/// </summary>
internal sealed class LocalRegistrySource : IKaleidoRegistrySource
{
    public Task<RegistrySourceContents> GetAsync(
        int? maxParallelism,
        CancellationToken cancellationToken) =>
        Task.FromResult(RegistrySourceContents.None);

    // Nothing is kept, so there is nothing to drop.
    public Task RefreshAsync(
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

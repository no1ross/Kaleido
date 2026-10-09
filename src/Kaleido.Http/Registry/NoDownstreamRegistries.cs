namespace Kaleido.Http.Registry;

/// <summary>
/// The default <see cref="IKaleidoDownstreamRegistries"/>: no downstream services. <c>AddHttpClients()</c>
/// replaces it with the clients' registries when clients are configured.
/// </summary>
internal sealed class NoDownstreamRegistries : IKaleidoDownstreamRegistries
{
    public Task<DownstreamRegistries> GetAsync(
        int? maxParallelism = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DownstreamRegistries.None);

    public void Invalidate()
    {
        // Nothing is cached when there are no downstream services.
    }
}

namespace Kaleido.Http.Registry;

/// <summary>
/// The default <see cref="IKaleidoDownstreamRegistries"/>: no downstream services. <c>AddHttpClients()</c>
/// replaces it with the clients' registries when clients are configured.
/// </summary>
internal sealed class NoDownstreamRegistries : IKaleidoDownstreamRegistries
{
    public Task<DownstreamRegistries> GetAsync(
        int? maxParallelism,
        CancellationToken cancellationToken) =>
        Task.FromResult(DownstreamRegistries.None);

    // Nothing is cached when there are no downstream services.
    public Task InvalidateAsync(
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    // No client calls any other service.
    public Task<AggregatedRegistryResponse?> GetServiceAsync(
        string serviceName,
        CancellationToken cancellationToken) =>
        Task.FromResult<AggregatedRegistryResponse?>(null);
}

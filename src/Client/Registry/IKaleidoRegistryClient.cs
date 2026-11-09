namespace Kaleido.Client.Registry;

/// <summary>Calls the central registry through the protocol chosen by the host.</summary>
public interface IKaleidoRegistryClient
{
    /// <summary>Reads the registry as the current caller may see it, with an optional ETag for a caller-scoped cache.</summary>
    Task<KaleidoRegistryDocument<KaleidoRegistry>> GetRegistryAsync(string? etag, CancellationToken cancellationToken);

    /// <summary>Reads the unfiltered capability definitions with the host's own credential.</summary>
    Task<KaleidoRegistryDocument<KaleidoRegistry>> GetCapabilitiesAsync(string? etag, CancellationToken cancellationToken);

    /// <summary>Reads the services' availability with the host's own credential.</summary>
    Task<KaleidoRegistryDocument<KaleidoRegistryAvailability>> GetAvailabilityAsync(string? etag, CancellationToken cancellationToken);

    /// <summary>Registers this service instance and receives the registry's lease.</summary>
    Task<RegistryLease> RegisterAsync(RegistryInstanceRegistration registration, CancellationToken cancellationToken);

    /// <summary>Renews an instance's lease; <see langword="null"/> means the registry no longer knows it.</summary>
    Task<RegistryLease?> HeartbeatAsync(Guid instanceId, RegistryHeartbeat heartbeat, CancellationToken cancellationToken);

    /// <summary>Deregisters this service instance.</summary>
    Task DeregisterAsync(Guid instanceId, CancellationToken cancellationToken);
}

/// <summary>The response to a conditional read of one registry document.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <param name="Value">The document, or <see langword="null"/> when unchanged.</param>
/// <param name="ETag">The ETag returned with the document, when present.</param>
/// <param name="NotModified">Whether the document was unchanged since the supplied ETag.</param>
public sealed record KaleidoRegistryDocument<T>(T? Value, string? ETag, bool NotModified) where T : class;

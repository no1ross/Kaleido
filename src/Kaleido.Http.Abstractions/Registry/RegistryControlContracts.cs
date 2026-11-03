using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.Registry;

/// <summary>
/// An instance registering with the control registry (<c>POST /registry/instances</c>, #553): who it is
/// and every capability it serves. The registry keeps one record per service and version; a second
/// instance of the same version adds itself to that record.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryInstanceRegistration
{
    /// <summary>The service (<see cref="KaleidoServiceOptions.ServiceName"/>).</summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// The service's version (<see cref="KaleidoServiceOptions.ServiceVersion"/>): required by the control
    /// registry, any label the team chooses.
    /// </summary>
    public required string ServiceVersion { get; init; }

    /// <summary>This instance (<see cref="KaleidoServiceOptions.InstanceId"/>).</summary>
    public required Guid InstanceId { get; init; }

    /// <summary>
    /// The address the service version is reached at (a DNS name or a load balancer, e.g.
    /// <c>https://radiology-2026-11.internal</c>), when the host configures one; the platform balances
    /// across instances behind it.
    /// </summary>
    public string? Address { get; init; }

    /// <summary>The instance's processors, as its own registry publishes them.</summary>
    public IReadOnlyCollection<ProcessorRegistryResponse> Processes { get; init; }
        = [];

    /// <summary>The instance's query sources, as its own registry publishes them.</summary>
    public IReadOnlyCollection<QueryableSourceResponse> Queryables { get; init; }
        = [];
}

/// <summary>
/// The control registry's answer to a registration or a heartbeat: the instance's lease.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryLease
{
    /// <summary>The instance the lease is for.</summary>
    public required Guid InstanceId { get; init; }

    /// <summary>How often the registry expects a heartbeat.</summary>
    public required TimeSpan HeartbeatInterval { get; init; }

    /// <summary>Until when the instance counts as alive without another heartbeat (UTC).</summary>
    public required DateTimeOffset LeaseUntilUtc { get; init; }
}

/// <summary>
/// An instance's heartbeat (<c>PUT /registry/instances/{instanceId}/heartbeat</c>): it renews the lease
/// and reports the instance's own health, from local checks only.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryHeartbeat
{
    /// <summary>Whether the instance's local checks pass; an unhealthy instance is not ready.</summary>
    public bool Healthy { get; init; } = true;
}

/// <summary>
/// Each service version's status (<c>GET /registry/status</c>, #553), apart from the capabilities: it
/// changes whenever an instance starts, stops or fails, while the capabilities change only with a deployment.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record KaleidoRegistryStatus
{
    /// <summary>Every service version the registry knows.</summary>
    public IReadOnlyCollection<RegistryServiceStatus> Services { get; init; } = [];

    /// <summary>When the statuses were worked out (UTC).</summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// A hash of <see cref="Services"/> (SHA-256, lowercase hex), mirrored by the <c>ETag</c> header: send it
    /// back as <c>If-None-Match</c> to get 304 while nothing changed.
    /// </summary>
    public string? Revision { get; init; }
}

/// <summary>A service version the control registry knows, and its instances' status (#553).</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryServiceStatus
{
    /// <summary>The service.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The version.</summary>
    public required string ServiceVersion { get; init; }

    /// <summary>
    /// Whether this is the service's current version: what a reference naming only the service reaches.
    /// The first version registered is current until it is changed.
    /// </summary>
    public bool IsCurrent { get; init; }

    /// <summary>The address the version is reached at, when one was registered.</summary>
    public string? Address { get; init; }

    /// <summary>The instances alive (leased) and reporting themselves healthy.</summary>
    public int ReadyInstances { get; init; }

    /// <summary>The instances known, ready or not, until they are removed after the retention period.</summary>
    public int Instances { get; init; }

    /// <summary>
    /// False when no instance is ready: the version's capabilities stay listed, but calls to it will fail.
    /// </summary>
    public bool Available { get; init; }
}

/// <summary>Something the control registry expects and has no registration for (#553).</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryMissingService
{
    /// <summary>The service.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The version expected; <see langword="null"/> when any version would do (its current one).</summary>
    public string? ServiceVersion { get; init; }

    /// <summary>
    /// Why it is expected: <c>expected</c> (configured), or <c>lookup</c> (a registered step looks up one of
    /// its query sources).
    /// </summary>
    public required string Reason { get; init; }
}

namespace Kaleido.Registry;

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

    /// <summary>The deployed service application's Product version, without its optional +commit suffix.</summary>
    public required string BuildVersion { get; init; }

    /// <summary>This instance (<see cref="KaleidoServiceOptions.InstanceId"/>).</summary>
    public required Guid InstanceId { get; init; }

    /// <summary>The instance's processors, as its own registry publishes them.</summary>
    public IReadOnlyCollection<ProcessRegistryItem> Processes { get; init; }
        = [];

    /// <summary>The instance's query sources, as its own registry publishes them.</summary>
    public IReadOnlyCollection<QueryableRegistryItem> Queryables { get; init; }
        = [];

    /// <summary>The protocols and public gateway routes for this service version; defaults to HTTP when empty.</summary>
    public IReadOnlyCollection<RegistryTransportRoute> Transports { get; init; } = [];
}

/// <summary>One protocol and its public logical gateway route, never an instance address.</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryTransportRoute
{
    /// <summary>The protocol name, such as <c>http</c> or <c>grpc</c>.</summary>
    public required string Transport { get; init; }

    /// <summary>The versioned gateway path that routes to this service, without an origin or query.</summary>
    public required string GatewayRoute { get; init; }
}

/// <summary>A service version and the public gateway routes its instances declare.</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryServiceRoute
{
    /// <summary>The service.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The version.</summary>
    public required string ServiceVersion { get; init; }

    /// <summary>The version's published protocols and their gateway paths.</summary>
    public IReadOnlyCollection<RegistryTransportRoute> Transports { get; init; } = [];
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
/// Each service version's status in detail, its instances included (<c>GET /registry/status</c>, #553): for
/// operations and the registry's UI. A capability consumer reads the overall status on each capability of
/// <see cref="KaleidoRegistry"/> instead (<see cref="RegistryStatusNames"/>). Kaleido hosts also use it
/// to keep their copy of the registry fresh: it changes whenever an instance starts, stops or fails, while
/// the capabilities change only with a deployment.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record KaleidoRegistryStatus
{
    /// <summary>Every service version the registry knows.</summary>
    public IReadOnlyCollection<RegistryServiceStatus> Services { get; init; } = [];

    /// <summary>When the statuses were worked out (UTC).</summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Opaque version of the statuses and instance states (heartbeat times do not change it).
    /// HTTP mirrors it as a weak <c>ETag</c>: send that header as <c>If-None-Match</c> while the state is unchanged.
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

    /// <summary>The version's overall status, as each of its capabilities carries it.</summary>
    public string Status { get; init; } = RegistryStatusNames.Unavailable;

    /// <summary>The version's published protocols and gateway paths.</summary>
    public IReadOnlyCollection<RegistryTransportRoute> Transports { get; init; } = [];

    /// <summary>The instances ready: alive (leased) and reporting themselves healthy.</summary>
    public int ReadyInstances { get; init; }

    /// <summary>The instances not heard from since the registry resumed watching after it was down.</summary>
    public int UnknownInstances { get; init; }

    /// <summary>Every instance known, ready or not, until it is removed after the retention period.</summary>
    public IReadOnlyCollection<RegistryInstanceStatus> Instances { get; init; } = [];
}

/// <summary>
/// Each service version's overall availability (<c>GET /registry/availability</c>, #553): what a Kaleido host
/// keeping its copy of the registry fresh needs, without the instances. Its revision changes when a
/// version's status, current version or published capabilities change - not when instances come and go within the same status. Where a
/// version runs is never the registry's: the router maps each version's route to its endpoint.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record KaleidoRegistryAvailability
{
    /// <summary>Every service version the registry knows.</summary>
    public IReadOnlyCollection<RegistryServiceAvailability> Services { get; init; } = [];

    /// <summary>
    /// How often a Kaleido host checks this availability again: the registry's heartbeat interval, decided by
    /// the registry, never by the host.
    /// </summary>
    public TimeSpan AvailabilityInterval { get; init; }

    /// <summary>
    /// The revision of the capabilities (<c>GET /registry/capabilities</c>) as they are now: a host holding
    /// other capabilities reads them at once, so a deployment shows everywhere within one availability check,
    /// and a host whose capabilities are current never reads them.
    /// </summary>
    public string? CapabilitiesRevision { get; init; }

    /// <summary>When the availability was worked out (UTC).</summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Opaque version of <see cref="Services"/>, the interval and the capabilities' version. HTTP mirrors
    /// it as a weak <c>ETag</c>: send that header as <c>If-None-Match</c> while availability is unchanged.
    /// </summary>
    public string? Revision { get; init; }
}

/// <summary>A service version and its overall availability (#553).</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryServiceAvailability
{
    /// <summary>The service.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The version.</summary>
    public required string ServiceVersion { get; init; }

    /// <summary>Whether this is the service's current version.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>The version's overall status, as each of its capabilities carries it.</summary>
    public string Status { get; init; } = RegistryStatusNames.Unavailable;

    /// <summary>The version's published protocols and gateway paths.</summary>
    public IReadOnlyCollection<RegistryTransportRoute> Transports { get; init; } = [];
}

/// <summary>An instance of a service version, as the control registry last heard from it (#553).</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryInstanceStatus
{
    /// <summary>The instance.</summary>
    public required Guid InstanceId { get; init; }

    /// <summary>Its state, worked out from its last heartbeat whenever the registry is read.</summary>
    public string State { get; init; } = RegistryInstanceStateNames.Unknown;

    /// <summary>Its last heartbeat, or its registration (UTC).</summary>
    public DateTimeOffset LastHeartbeatUtc { get; init; }
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

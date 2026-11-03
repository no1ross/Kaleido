namespace Kaleido.Http.Registry;

/// <summary>
/// The control registry's state (#553): one registration per service version and revision, every instance
/// with its last heartbeat, and each service's current version. In memory (<c>AddInMemoryRegistryStore()</c>)
/// or durable (e.g. <c>AddSqliteRegistryStore</c>, or your own with <c>AddRegistryStore&lt;T&gt;()</c>), shared
/// by every registry node.
/// </summary>
/// <remarks>
/// <para>
/// The store keeps what it is given; the rules (leases, the rolling-deployment rule, completeness) are the
/// registry's. A platform that edits the registry plugs in its own store over its own database.
/// </para>
/// <para>
/// Several registry nodes share one store, and many service instances write to it at once (a few
/// hundred instances heartbeat every few seconds), so every write is atomic on its own:
/// a registration is inserted only if absent; the current version is set only if absent; a renewal and a
/// removal are each one conditional statement; a sweep may run on every registry node at once.
/// </para>
/// </remarks>
public interface IKaleidoRegistryStore
{
    /// <summary>
    /// Adds a registration with its capabilities: what one build of a service version publishes. A
    /// registration with the same revision already stored is kept as it is (insert-if-absent).
    /// </summary>
    /// <param name="registration">The registration.</param>
    /// <param name="capabilities">Its capabilities, as the registry serialized them; opaque to the store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task AddRegistrationAsync(RegistryRegistrationRecord registration, string capabilities, CancellationToken cancellationToken);

    /// <summary>Saves an instance at its registration, replacing the instance with the same id.</summary>
    Task SaveInstanceAsync(RegistryInstanceRecord instance, CancellationToken cancellationToken);

    /// <summary>
    /// Renews an instance's lease: its last heartbeat and its health, in one conditional statement, only
    /// when the instance is one of <paramref name="allowedServices"/> (any service when null).
    /// </summary>
    /// <param name="instanceId">The instance.</param>
    /// <param name="heartbeatUtc">The heartbeat's time, on the registry's clock.</param>
    /// <param name="healthy">Whether the instance reported its local checks healthy.</param>
    /// <param name="allowedServices">The services the caller may renew; null when anyone may.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done; Unknown when there is no such instance; NotAllowed when it is another service's.</returns>
    Task<RegistryInstanceChange> RenewInstanceAsync(Guid instanceId, DateTimeOffset heartbeatUtc, bool healthy, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken);

    /// <summary>Removes an instance, under the same condition as <see cref="RenewInstanceAsync"/>.</summary>
    /// <param name="instanceId">The instance.</param>
    /// <param name="allowedServices">The services the caller may remove; null when anyone may.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done; Unknown when there is no such instance; NotAllowed when it is another service's.</returns>
    Task<RegistryInstanceChange> RemoveInstanceAsync(Guid instanceId, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the instances whose last heartbeat is older than <paramref name="cutoffUtc"/>, then the
    /// registrations no instance runs; returns the instances removed. Idempotent.
    /// </summary>
    Task<int> RemoveInstancesSeenBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);

    /// <summary>
    /// The service's current version, setting it to <paramref name="serviceVersion"/> when the service has
    /// none (its first registration). An existing current version is never changed here.
    /// </summary>
    Task<string> GetOrSetCurrentVersionAsync(string serviceName, string serviceVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Keeps the registry's watch alive, in one atomic statement: every registry node touches it
    /// regularly, so while one is running the watch is unbroken. When no registry node touched it within
    /// <paramref name="lease"/> (every one was down), the watch resumes at <paramref name="nowUtc"/>: service
    /// instances not heard from since are Unknown, and their silence is not counted against them.
    /// </summary>
    /// <param name="nowUtc">The touch's time, on the registry's clock.</param>
    /// <param name="lease">How long the watch stays unbroken without a touch.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>When the current unbroken watch began (UTC).</returns>
    Task<DateTimeOffset> TouchWatchAsync(DateTimeOffset nowUtc, TimeSpan lease, CancellationToken cancellationToken);

    /// <summary>
    /// The registrations (without their capabilities), the instances, each service's current version and
    /// when the watch resumed, for the registry to build its view.
    /// </summary>
    Task<RegistryStoreContents> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The capabilities of the revisions asked for, by revision; a revision the store no longer has is
    /// left out. A revision never changes, so the registry keeps what it read.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetCapabilitiesAsync(IReadOnlyCollection<string> revisions, CancellationToken cancellationToken);
}

/// <summary>The outcome of a conditional renewal or removal of an instance.</summary>
public enum RegistryInstanceChange
{
    /// <summary>Renewed or removed.</summary>
    Done,

    /// <summary>There is no such instance.</summary>
    Unknown,

    /// <summary>The instance is a service the caller may not register.</summary>
    NotAllowed,
}

/// <summary>A registration: one build (revision) of a service version.</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryRegistrationRecord
{
    /// <summary>The service.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The version.</summary>
    public required string ServiceVersion { get; init; }

    /// <summary>A hash of the capabilities: instances of the same build share it.</summary>
    public required string Revision { get; init; }

    /// <summary>When the revision was first registered (UTC).</summary>
    public required DateTimeOffset RegisteredUtc { get; init; }
}

/// <summary>An instance of a service version, and its last heartbeat.</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryInstanceRecord
{
    /// <summary>The instance.</summary>
    public required Guid InstanceId { get; init; }

    /// <summary>The service.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The version.</summary>
    public required string ServiceVersion { get; init; }

    /// <summary>The revision (build) it runs.</summary>
    public required string Revision { get; init; }

    /// <summary>The address its version is reached at, when it registered one.</summary>
    public string? Address { get; init; }

    /// <summary>Whether its last heartbeat reported its local checks healthy.</summary>
    public bool Healthy { get; init; } = true;

    /// <summary>When it registered (UTC).</summary>
    public required DateTimeOffset RegisteredUtc { get; init; }

    /// <summary>Its last heartbeat, or its registration (UTC).</summary>
    public required DateTimeOffset LastHeartbeatUtc { get; init; }
}

/// <summary>What a registry store holds, without the registrations' capabilities.</summary>
[ExcludeFromCodeCoverage]
public sealed record RegistryStoreContents
{
    /// <summary>The registrations.</summary>
    public IReadOnlyCollection<RegistryRegistrationRecord> Registrations { get; init; } = [];

    /// <summary>The instances.</summary>
    public IReadOnlyCollection<RegistryInstanceRecord> Instances { get; init; } = [];

    /// <summary>Each service's current version, by service name.</summary>
    public IReadOnlyDictionary<string, string> CurrentVersions { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>When the registry's current unbroken watch began (UTC); null before the first touch.</summary>
    public DateTimeOffset? WatchResumedUtc { get; init; }
}

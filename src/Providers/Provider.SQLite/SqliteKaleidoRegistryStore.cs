using Kaleido.Provider.SQLite.Entities;
using Kaleido.Registry;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

// The control registry's store on SQLite (#553). Every write is one statement, so it is atomic among
// many instances and several registry nodes on the same database: a registration and the current
// version are inserted only if absent (ON CONFLICT DO NOTHING), a renewal and a removal check the
// instance's service in their own WHERE, and the sweep may run anywhere at once.
internal sealed class SqliteKaleidoRegistryStore(SqliteKaleidoRegistryDbContext dbContext)
    : IKaleidoRegistryStore
{
    public async Task AddRegistrationAsync(RegistryRegistrationRecord registration, string capabilities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var registered = registration.RegisteredUtc.UtcTicks;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO RegistryRegistrations (Revision, ServiceName, ServiceVersion, Capabilities, RegisteredUtc)
            VALUES ({registration.Revision}, {registration.ServiceName}, {registration.ServiceVersion}, {capabilities}, {registered})
            ON CONFLICT(Revision) DO NOTHING
            """,
            cancellationToken);
    }

    public async Task SaveInstanceAsync(RegistryInstanceRecord instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var id = Id(instance.InstanceId);
        var registered = instance.RegisteredUtc.UtcTicks;
        var heartbeat = instance.LastHeartbeatUtc.UtcTicks;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO RegistryInstances (InstanceId, ServiceName, ServiceVersion, Revision, Healthy, RegisteredUtc, LastHeartbeatUtc)
            VALUES ({id}, {instance.ServiceName}, {instance.ServiceVersion}, {instance.Revision}, {instance.Healthy}, {registered}, {heartbeat})
            ON CONFLICT(InstanceId) DO UPDATE SET
                ServiceName = excluded.ServiceName, ServiceVersion = excluded.ServiceVersion, Revision = excluded.Revision,
                Healthy = excluded.Healthy, RegisteredUtc = excluded.RegisteredUtc,
                LastHeartbeatUtc = excluded.LastHeartbeatUtc
            """,
            cancellationToken);
    }

    public async Task<RegistryInstanceChange> RenewInstanceAsync(Guid instanceId, DateTimeOffset heartbeatUtc, bool healthy, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken)
    {
        var heartbeat = heartbeatUtc.UtcTicks;

        var renewed = await Instance(instanceId, allowedServices).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(x => x.LastHeartbeatUtc, heartbeat)
                .SetProperty(x => x.Healthy, healthy),
            cancellationToken);

        return renewed > 0 ? RegistryInstanceChange.Done : await RefusedAsync(instanceId, cancellationToken);
    }

    public async Task<RegistryInstanceChange> RemoveInstanceAsync(Guid instanceId, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken)
    {
        var removed = await Instance(instanceId, allowedServices).ExecuteDeleteAsync(cancellationToken);

        return removed > 0 ? RegistryInstanceChange.Done : await RefusedAsync(instanceId, cancellationToken);
    }

    public async Task<int> RemoveInstancesSeenBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        var cutoff = cutoffUtc.UtcTicks;
        var removed = await dbContext.RegistryInstances.Where(x => x.LastHeartbeatUtc < cutoff).ExecuteDeleteAsync(cancellationToken);

        // A registration no instance runs any more goes with its last instance.
        await dbContext.RegistryRegistrations
            .Where(r => !dbContext.RegistryInstances.Any(i => i.Revision == r.Revision))
            .ExecuteDeleteAsync(cancellationToken);

        return removed;
    }

    public async Task<string> GetOrSetCurrentVersionAsync(string serviceName, string serviceVersion, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO RegistryCurrentVersions (ServiceName, ServiceVersion) VALUES ({serviceName}, {serviceVersion})
            ON CONFLICT(ServiceName) DO NOTHING
            """,
            cancellationToken);

        return await dbContext.RegistryCurrentVersions.AsNoTracking()
            .Where(x => x.ServiceName == serviceName)
            .Select(x => x.ServiceVersion)
            .FirstAsync(cancellationToken);
    }

    // One statement: the watch resumes at now when no registry node touched it within the lease.
    public async Task<DateTimeOffset> TouchWatchAsync(DateTimeOffset nowUtc, TimeSpan lease, CancellationToken cancellationToken)
    {
        var now = nowUtc.UtcTicks;
        var gap = now - lease.Ticks;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO RegistryWatch (Id, ResumedUtc, LastSeenUtc) VALUES (1, {now}, {now})
            ON CONFLICT(Id) DO UPDATE SET
                ResumedUtc = CASE WHEN LastSeenUtc < {gap} THEN {now} ELSE ResumedUtc END,
                LastSeenUtc = MAX(LastSeenUtc, {now})
            """,
            cancellationToken);

        return Utc(await dbContext.RegistryWatch.AsNoTracking().Where(x => x.Id == 1).Select(x => x.ResumedUtc).FirstAsync(cancellationToken));
    }

    public async Task<RegistryStoreContents> ReadAsync(CancellationToken cancellationToken)
    {
        var registrations = await dbContext.RegistryRegistrations.AsNoTracking()
            .Select(x => new { x.ServiceName, x.ServiceVersion, x.Revision, x.RegisteredUtc })
            .ToListAsync(cancellationToken);
        var instances = await dbContext.RegistryInstances.AsNoTracking().ToListAsync(cancellationToken);
        var current = await dbContext.RegistryCurrentVersions.AsNoTracking().ToListAsync(cancellationToken);
        var resumed = await dbContext.RegistryWatch.AsNoTracking().Where(x => x.Id == 1).Select(x => (long?)x.ResumedUtc).FirstOrDefaultAsync(cancellationToken);

        return new RegistryStoreContents
        {
            Registrations = [.. registrations.Select(x => new RegistryRegistrationRecord { ServiceName = x.ServiceName, ServiceVersion = x.ServiceVersion, Revision = x.Revision, RegisteredUtc = Utc(x.RegisteredUtc) })],
            Instances = [.. instances.Select(ToRecord)],
            CurrentVersions = current.ToDictionary(x => x.ServiceName, x => x.ServiceVersion, StringComparer.OrdinalIgnoreCase),
            WatchResumedUtc = resumed is { } ticks ? Utc(ticks) : null,
        };
    }

    public async Task<IReadOnlyDictionary<string, string>> GetCapabilitiesAsync(IReadOnlyCollection<string> revisions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revisions);

        if (revisions.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return await dbContext.RegistryRegistrations.AsNoTracking()
            .Where(x => revisions.Contains(x.Revision))
            .ToDictionaryAsync(x => x.Revision, x => x.Capabilities, StringComparer.Ordinal, cancellationToken);
    }

    // The instance, if it is one of the services the caller may change (any, when null). The service
    // column compares without case (NOCASE), as service names do everywhere in Kaleido.
    private IQueryable<RegistryInstanceEntity> Instance(Guid instanceId, IReadOnlyCollection<string>? allowedServices)
    {
        var id = Id(instanceId);
        var instance = dbContext.RegistryInstances.Where(x => x.InstanceId == id);

        return allowedServices is null
            ? instance
            : instance.Where(x => allowedServices.Contains(x.ServiceName));
    }

    // Only after the conditional write changed nothing: was there no instance, or another service's?
    private async Task<RegistryInstanceChange> RefusedAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var id = Id(instanceId);

        return await dbContext.RegistryInstances.AnyAsync(x => x.InstanceId == id, cancellationToken)
            ? RegistryInstanceChange.NotAllowed
            : RegistryInstanceChange.Unknown;
    }

    private static RegistryInstanceRecord ToRecord(RegistryInstanceEntity x) =>
        new()
        {
            InstanceId = Guid.ParseExact(x.InstanceId, "N"),
            ServiceName = x.ServiceName,
            ServiceVersion = x.ServiceVersion,
            Revision = x.Revision,
            Healthy = x.Healthy,
            RegisteredUtc = Utc(x.RegisteredUtc),
            LastHeartbeatUtc = Utc(x.LastHeartbeatUtc),
        };

    private static string Id(Guid instanceId) => instanceId.ToString("N");

    private static DateTimeOffset Utc(long ticks) => new(ticks, TimeSpan.Zero);
}

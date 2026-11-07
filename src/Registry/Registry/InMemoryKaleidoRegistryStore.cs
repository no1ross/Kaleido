using Microsoft.Extensions.Logging;

namespace Kaleido.Registry;

// The control registry's state in this process only: for development and tests, chosen explicitly with
// AddInMemoryRegistryStore(). Lost on restart, and not shared between registry nodes. Every operation
// takes one lock, so each is atomic as the contract requires.
internal sealed partial class InMemoryKaleidoRegistryStore : IKaleidoRegistryStore
{
    private readonly Dictionary<string, (RegistryRegistrationRecord Registration, string Capabilities)> _registrations = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, RegistryInstanceRecord> _instances = [];
    private readonly Dictionary<string, string> _current = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private (DateTimeOffset ResumedUtc, DateTimeOffset LastSeenUtc)? _watch;

    public InMemoryKaleidoRegistryStore(ILogger<InMemoryKaleidoRegistryStore> logger) => LogInMemoryStoreActive(logger);

    public Task AddRegistrationAsync(RegistryRegistrationRecord registration, string capabilities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (_lock)
        {
            _registrations.TryAdd(registration.Revision, (registration, capabilities));
        }

        return Task.CompletedTask;
    }

    public Task SaveInstanceAsync(RegistryInstanceRecord instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (_lock)
        {
            _instances[instance.InstanceId] = instance;
        }

        return Task.CompletedTask;
    }

    public Task<RegistryInstanceChange> RenewInstanceAsync(Guid instanceId, DateTimeOffset heartbeatUtc, bool healthy, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var change = Change(instanceId, allowedServices);

            if (change == RegistryInstanceChange.Done)
            {
                _instances[instanceId] = _instances[instanceId] with { LastHeartbeatUtc = heartbeatUtc, Healthy = healthy };
            }

            return Task.FromResult(change);
        }
    }

    public Task<RegistryInstanceChange> RemoveInstanceAsync(Guid instanceId, IReadOnlyCollection<string>? allowedServices, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var change = Change(instanceId, allowedServices);

            if (change == RegistryInstanceChange.Done)
            {
                _instances.Remove(instanceId);
            }

            return Task.FromResult(change);
        }
    }

    public Task<int> RemoveInstancesSeenBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var stale = _instances.Values.Where(x => x.LastHeartbeatUtc < cutoffUtc).Select(x => x.InstanceId).ToList();

            foreach (var id in stale)
            {
                _instances.Remove(id);
            }

            // A registration no instance runs any more goes with its last instance.
            var running = _instances.Values.Select(x => x.Revision).ToHashSet(StringComparer.Ordinal);

            foreach (var revision in _registrations.Keys.Where(x => !running.Contains(x)).ToList())
            {
                _registrations.Remove(revision);
            }

            return Task.FromResult(stale.Count);
        }
    }

    public Task<string> GetOrSetCurrentVersionAsync(string serviceName, string serviceVersion, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_current.TryAdd(serviceName, serviceVersion) ? serviceVersion : _current[serviceName]);
        }
    }

    public Task<DateTimeOffset> TouchWatchAsync(DateTimeOffset nowUtc, TimeSpan lease, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var resumed = _watch is { } watch && watch.LastSeenUtc >= nowUtc - lease ? watch.ResumedUtc : nowUtc;
            _watch = (resumed, _watch is { } last && last.LastSeenUtc > nowUtc ? last.LastSeenUtc : nowUtc);

            return Task.FromResult(resumed);
        }
    }

    public Task<RegistryStoreContents> ReadAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(new RegistryStoreContents
            {
                Registrations = [.. _registrations.Values.Select(x => x.Registration)],
                Instances = [.. _instances.Values],
                CurrentVersions = new Dictionary<string, string>(_current, StringComparer.OrdinalIgnoreCase),
                WatchResumedUtc = _watch?.ResumedUtc,
            });
        }
    }

    public Task<IReadOnlyDictionary<string, string>> GetCapabilitiesAsync(IReadOnlyCollection<string> revisions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revisions);

        lock (_lock)
        {
            IReadOnlyDictionary<string, string> found = revisions
                .Where(_registrations.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(x => x, x => _registrations[x].Capabilities, StringComparer.Ordinal);

            return Task.FromResult(found);
        }
    }

    // Under the lock: whether the instance exists and is one of the services the caller may change.
    private RegistryInstanceChange Change(Guid instanceId, IReadOnlyCollection<string>? allowedServices) =>
        !_instances.TryGetValue(instanceId, out var instance)
            ? RegistryInstanceChange.Unknown
            : allowedServices?.Contains(instance.ServiceName, StringComparer.OrdinalIgnoreCase) != false
                ? RegistryInstanceChange.Done
                : RegistryInstanceChange.NotAllowed;

    [LoggerMessage(Level = LogLevel.Warning, Message = "InMemoryKaleidoRegistryStore is active. The control registry's registrations and instances are kept in memory only: lost on restart, and not shared between registry nodes. Register a durable registry store (e.g. AddSqliteRegistryStore, or AddRegistryStore<T>()) before deploying to production.")]
    private static partial void LogInMemoryStoreActive(ILogger logger);
}

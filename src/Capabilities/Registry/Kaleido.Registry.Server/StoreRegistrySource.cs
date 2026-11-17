using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Registry.Server;

// The control registry's full registry, from its store (#553). HybridCache shares each immutable
// build's published capabilities and the briefly cached view across the registry, status and health reads.
// The store remains the source of truth after a cache miss.
// The rules, worked out on every build from what the instances reported - nothing writes a state:
// - each instance's state: Unknown while it has not been heard from since the registry's watch resumed
//   (every registry node was down), for one lease; else Ready (healthy, a heartbeat within its lease)
//   or NotReady;
// - each service version's capabilities: during a rolling deployment only what every live (ready or
//   unknown) instance's revision shares (by name), as the longest-running revision defines it; with no live
//   instance, what its known instances run, so the version stays listed (unavailable);
// - each version's overall status (Available, Degraded, Unknown, Unavailable), carried on each of its
//   capabilities, and its instances in detail;
// - what is missing: an expected processor version, or a service a published step looks up, with no
//   registration.
// What only the registry itself has (#553): each version's status in detail, its instances included, and
// the capabilities alone - for its status and capabilities endpoints and its health check.
internal interface IRegistryStatusReader
{
    Task<KaleidoRegistryStatus> GetStatusAsync(CancellationToken cancellationToken);

    Task<KaleidoRegistry> GetCapabilitiesAsync(CancellationToken cancellationToken);
}

internal sealed class StoreRegistrySource(
    IServiceScopeFactory scopeFactory,
    IRegistryCache cache,
    KaleidoRegistryOptions options,
    TimeProvider time)
    : IKaleidoRegistrySource, IRegistryStatusReader
{
    private readonly object _versionGate = new();
    private string[] _capabilityKey = [];
    private string[] _statusKey = [];
    private string[] _availabilityKey = [];
    private string[] _registryKey = [];
    private string _capabilityVersion = Guid.NewGuid().ToString("N");
    private string _statusVersion = Guid.NewGuid().ToString("N");
    private string _availabilityVersion = Guid.NewGuid().ToString("N");
    private string _registryVersion = Guid.NewGuid().ToString("N");

    // The store's registry is the whole registry: every participant registered its own capabilities.
    // The registry a consumer reads: every capability with its version's overall status.
    public async Task<KaleidoRegistry> GetAsync(CancellationToken cancellationToken) =>
        (await ViewAsync(cancellationToken)).Contents;

    // The capabilities alone, for the hosts that keep their copy fresh: they change only with a deployment.
    public async Task<KaleidoRegistry> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
        (await ViewAsync(cancellationToken)).Capabilities;

    public async Task<KaleidoRegistryStatus> GetStatusAsync(CancellationToken cancellationToken) =>
        (await ViewAsync(cancellationToken)).Status;

    // Each version's overall availability, for the hosts keeping their copy fresh: no instances.
    public async Task<KaleidoRegistryAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
        (await ViewAsync(cancellationToken)).Availability;

    public Task RefreshAsync(CancellationToken cancellationToken) => cache.InvalidateViewAsync(cancellationToken);

    private Task<RegistryView> ViewAsync(CancellationToken cancellationToken) => cache.GetViewAsync(BuildAsync, cancellationToken);

    private async Task<RegistryView> BuildAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IKaleidoRegistryStore>();
        var contents = await store.ReadAsync(cancellationToken);
        var published = await LoadCapabilitiesAsync(store, contents, cancellationToken);

        var now = time.GetUtcNow();
        var (capabilities, statuses) = Build(contents, now, published);
        var capabilitiesVersion = Version(ref _capabilityKey, ref _capabilityVersion, CapabilityKey(contents, now));
        var statusVersion = Version(ref _statusKey, ref _statusVersion, StatusKey(statuses, includeInstances: true));
        var availabilityVersion = Version(ref _availabilityKey, ref _availabilityVersion,
            [capabilitiesVersion, options.HeartbeatInterval.Ticks.ToString(CultureInfo.InvariantCulture), .. StatusKey(statuses, includeInstances: false)]);
        var registryVersion = Version(ref _registryKey, ref _registryVersion, [capabilitiesVersion, availabilityVersion]);
        var revisioned = capabilities with { Revision = capabilitiesVersion };
        var status = new KaleidoRegistryStatus { Services = statuses, GeneratedAt = now, Revision = statusVersion };
        List<RegistryServiceAvailability> versions = [.. statuses.Select(x => new RegistryServiceAvailability { ServiceName = x.ServiceName, ServiceVersion = x.ServiceVersion, IsCurrent = x.IsCurrent, Status = x.Status, Transports = x.Transports })];
        var availability = new KaleidoRegistryAvailability
        {
            Services = versions,
            AvailabilityInterval = options.HeartbeatInterval,
            CapabilitiesRevision = capabilitiesVersion,
            GeneratedAt = now,
            Revision = availabilityVersion,
        };

        return new RegistryView(
            revisioned,
            revisioned.WithAvailability(availability) with { Revision = registryVersion },
            status,
            availability);
    }

    // A published build is immutable: its cached model stays valid when an instance stops or a version rolls back.
    private async Task<IReadOnlyDictionary<(string Service, string Version, string Build), KaleidoRegistry>> LoadCapabilitiesAsync(IKaleidoRegistryStore store, RegistryStoreContents contents, CancellationToken cancellationToken)
    {
        var published = new Dictionary<(string Service, string Version, string Build), KaleidoRegistry>();
        foreach (var registration in contents.Registrations.DistinctBy(x => (x.ServiceName, x.ServiceVersion, x.BuildVersion)))
        {
            try
            {
                var key = (registration.ServiceName, registration.ServiceVersion, registration.BuildVersion);
                var cacheKey = $"{key.ServiceName}@{key.ServiceVersion}@{key.BuildVersion}";
                published[key] = await cache.GetCapabilitiesAsync(cacheKey, async token =>
                {
                    var stored = await store.GetCapabilitiesAsync(key.ServiceName, key.ServiceVersion, [key.BuildVersion], token);
                    return stored[key.BuildVersion];
                }, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                continue;
            }
        }

        return published;
    }

    private (KaleidoRegistry Registry, List<RegistryServiceStatus> Statuses) Build(RegistryStoreContents contents, DateTimeOffset now, IReadOnlyDictionary<(string Service, string Version, string Build), KaleidoRegistry> published)
    {
        var versions = contents.Registrations.Select(x => (x.ServiceName, x.ServiceVersion))
            .Concat(contents.Instances.Select(x => (x.ServiceName, x.ServiceVersion)))
            .DistinctBy(x => $"{x.ServiceName.ToLowerInvariant()}/{x.ServiceVersion}", StringComparer.Ordinal)
            .OrderBy(x => x.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ServiceVersion, StringComparer.Ordinal)
            .ToList();

        var processes = new List<ProcessRegistryItem>();
        var queryables = new List<QueryableRegistryItem>();
        var routes = new List<RegistryServiceRoute>();
        var statuses = new List<RegistryServiceStatus>();

        foreach (var (service, version) in versions)
        {
            var instances = contents.Instances
                .Where(x => Is(x.ServiceName, x.ServiceVersion, service, version))
                .Select(x => (Record: x, State: StateOf(x, now, contents.WatchResumedUtc)))
                .ToList();
            var live = instances.Where(x => !string.Equals(x.State, RegistryInstanceStateNames.NotReady, StringComparison.Ordinal)).Select(x => x.Record).ToList();
            var shared = Published(contents.Registrations.Where(x => Is(x.ServiceName, x.ServiceVersion, service, version)), live.Count > 0 ? live : [.. instances.Select(x => x.Record)], published, service, version);

            // Each entry is the version it was registered under, whatever the stored copy says.
            processes.AddRange(shared.Processes.Select(x => x with { ServiceVersion = version }));
            queryables.AddRange(shared.Queryables.Select(x => x with { ServiceVersion = version }));
            routes.AddRange(shared.Routes);
            statuses.Add(StatusOf(service, version, instances, contents.CurrentVersions, shared.Routes.Single().Transports));
        }

        return (new KaleidoRegistry { Processes = processes, Queryables = queryables, Routes = routes, Missing = Missing(versions, processes) }, statuses);
    }

    // Unknown while not heard from since the watch resumed, for one lease after it; else by lease and health.
    private string StateOf(RegistryInstanceRecord instance, DateTimeOffset now, DateTimeOffset? resumed) =>
        resumed is { } since && instance.LastHeartbeatUtc < since && now < since + options.Lease
            ? RegistryInstanceStateNames.Unknown
            : instance.Healthy && now - instance.LastHeartbeatUtc <= options.Lease
                ? RegistryInstanceStateNames.Ready
                : RegistryInstanceStateNames.NotReady;

    private static RegistryServiceStatus StatusOf(string service, string version, List<(RegistryInstanceRecord Record, string State)> instances, IReadOnlyDictionary<string, string> currentVersions, IReadOnlyCollection<RegistryTransportRoute> transports)
    {
        var ready = instances.Count(x => string.Equals(x.State, RegistryInstanceStateNames.Ready, StringComparison.Ordinal));
        var unknown = instances.Count(x => string.Equals(x.State, RegistryInstanceStateNames.Unknown, StringComparison.Ordinal));

        return new RegistryServiceStatus
        {
            ServiceName = service,
            ServiceVersion = version,
            IsCurrent = currentVersions.TryGetValue(service, out var current) && string.Equals(current, version, StringComparison.Ordinal),
            Transports = transports,
            Status = instances.Count == 0 || transports.Count == 0 ? RegistryStatusNames.Unavailable
                : ready == instances.Count ? RegistryStatusNames.Available
                : ready > 0 ? RegistryStatusNames.Degraded
                : unknown > 0 ? RegistryStatusNames.Unknown
                : RegistryStatusNames.Unavailable,
            ReadyInstances = ready,
            UnknownInstances = unknown,
            Instances = [.. instances
                .OrderBy(x => x.Record.InstanceId)
                .Select(x => new RegistryInstanceStatus { InstanceId = x.Record.InstanceId, State = x.State, LastHeartbeatUtc = x.Record.LastHeartbeatUtc })],
        };
    }

    // The registrations the instances run, oldest first; with no instance, every registration kept.
    private static KaleidoRegistry Published(IEnumerable<RegistryRegistrationRecord> registrations, List<RegistryInstanceRecord> instances, IReadOnlyDictionary<(string Service, string Version, string Build), KaleidoRegistry> capabilities, string service, string version)
    {
        var running = instances.Select(x => x.BuildVersion).ToHashSet(StringComparer.Ordinal);
        var all = registrations.OrderBy(x => x.RegisteredUtc).ToList();
        var used = all.Where(x => running.Contains(x.BuildVersion)).ToList();

        return Shared([.. (used.Count > 0 ? used : all).Select(x => capabilities.GetValueOrDefault((x.ServiceName, x.ServiceVersion, x.BuildVersion))).OfType<KaleidoRegistry>()], service, version);
    }

    // The rolling-deployment rule: a capability every live build has, as the oldest one defines it.
    private static KaleidoRegistry Shared(List<KaleidoRegistry> builds, string service, string version)
    {
        var oldest = builds.FirstOrDefault() ?? new KaleidoRegistry();
        var transports = TransportsOf(oldest, service, version)
            .Where(x => builds.Skip(1).All(r => TransportsOf(r, service, version).Any(other => string.Equals(x.Transport, other.Transport, StringComparison.OrdinalIgnoreCase))))
            .ToArray();
        var routes = new RegistryServiceRoute { ServiceName = service, ServiceVersion = version, Transports = transports };
        if (builds.Count <= 1)
        {
            return oldest with { Routes = [routes] };
        }

        var processes = oldest.Processes
            .Where(p => builds.TrueForAll(r => r.Processes.Any(o => SameName(o.Name, p.Name))))
            .Select(p => p with
            {
                InitialSteps = [.. p.InitialSteps.Where(s => builds.TrueForAll(r => ProcessorOf(r, p.Name).InitialSteps.Any(o => SameName(o.Name, s.Name))))],
                Steps = p.Steps is null ? null : [.. p.Steps.Where(s => builds.TrueForAll(r => (ProcessorOf(r, p.Name).Steps ?? []).Any(o => SameName(o.Name, s.Name))))],
            });

        var queryables = oldest.Queryables
            .Where(q => builds.TrueForAll(r => r.Queryables.Any(o => SameName(o.Name, q.Name))))
            .Select(q => q with
            {
                Views = [.. q.Views.Where(v => builds.TrueForAll(r => SourceOf(r, q.Name).Views.Any(o => SameName(o.Name, v.Name))))],
            });

        return new KaleidoRegistry { Processes = [.. processes], Queryables = [.. queryables], Routes = [routes] };
    }

    private static IReadOnlyCollection<RegistryTransportRoute> TransportsOf(KaleidoRegistry registry, string service, string version) =>
        registry.Routes.FirstOrDefault(x => Is(x.ServiceName, x.ServiceVersion, service, version))?.Transports is { Count: > 0 } declared
            ? declared
            : [new RegistryTransportRoute { Transport = "http", GatewayRoute = $"/{service}/{version}" }];

    private List<RegistryMissingService> Missing(List<(string ServiceName, string ServiceVersion)> versions, List<ProcessRegistryItem> processes)
    {
        var expected = options.Expected
            .SelectMany(x => x.Versions.Select(v => (x.ServiceName, Version: v)))
            .Where(x => !versions.Exists(v => Is(v.ServiceName, v.ServiceVersion, x.ServiceName, x.Version)))
            .Select(x => new RegistryMissingService { ServiceName = x.ServiceName, ServiceVersion = x.Version, Reason = "expected" });

        var lookups = processes
            .SelectMany(p => p.Steps ?? [])
            .SelectMany(s => s.Fields)
            .Select(f => f.Lookup?.Service)
            .OfType<string>()
            .Where(service => !versions.Exists(v => SameName(v.ServiceName, service)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(service => new RegistryMissingService { ServiceName = service, Reason = "lookup" });

        return [.. expected.Concat(lookups)];
    }

    private static ProcessRegistryItem ProcessorOf(KaleidoRegistry capabilities, string name) =>
        capabilities.Processes.First(x => SameName(x.Name, name));

    private static QueryableRegistryItem SourceOf(KaleidoRegistry capabilities, string name) =>
        capabilities.Queryables.First(x => SameName(x.Name, name));

    private string[] CapabilityKey(RegistryStoreContents contents, DateTimeOffset now)
    {
        var builds = contents.Registrations
            .Select(x => $"{x.ServiceName}@{x.ServiceVersion}@{x.BuildVersion}")
            .OrderBy(x => x, StringComparer.Ordinal);
        var selected = contents.Instances
            .GroupBy(x => (x.ServiceName, x.ServiceVersion))
            .SelectMany(group =>
            {
                var live = group.Where(x => !string.Equals(StateOf(x, now, contents.WatchResumedUtc), RegistryInstanceStateNames.NotReady, StringComparison.Ordinal)).ToArray();
                IEnumerable<RegistryInstanceRecord> chosen = live.Length > 0 ? live : group;
                return chosen.Select(x => $"{x.ServiceName}@{x.ServiceVersion}@{x.BuildVersion}");
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal);
        return [.. builds, "selected", .. selected];
    }

    private static string[] StatusKey(IEnumerable<RegistryServiceStatus> statuses, bool includeInstances)
    {
        var key = new List<string>();
        foreach (var service in statuses)
        {
            key.Add(service.ServiceName);
            key.Add(service.ServiceVersion);
            key.Add(service.IsCurrent ? "current" : "previous");
            key.Add(service.Status);
            key.Add(service.Transports.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var transport in service.Transports)
            {
                key.Add(transport.Transport);
                key.Add(transport.GatewayRoute);
            }

            if (includeInstances)
            {
                key.Add(service.Instances.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var instance in service.Instances)
                {
                    key.Add(instance.InstanceId.ToString("N"));
                    key.Add(instance.State);
                }
            }
        }

        return [.. key];
    }

    private string Version(ref string[] previous, ref string version, string[] current)
    {
        lock (_versionGate)
        {
            if (!previous.SequenceEqual(current, StringComparer.Ordinal))
            {
                previous = current;
                version = Guid.NewGuid().ToString("N");
            }

            return version;
        }
    }

    private static bool Is(string service, string version, string otherService, string otherVersion) =>
        SameName(service, otherService) && string.Equals(version, otherVersion, StringComparison.Ordinal);

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

}

internal sealed record RegistryView(KaleidoRegistry Capabilities, KaleidoRegistry Contents, KaleidoRegistryStatus Status, KaleidoRegistryAvailability Availability);

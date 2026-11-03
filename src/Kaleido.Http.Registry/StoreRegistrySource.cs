using System.Collections.Concurrent;
using System.Text.Json;
using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Registry;

// The control registry's full registry, from its store (#553). One per host: it keeps each revision's
// parsed capabilities (a revision never changes, so only new ones are read), and the view it built for
// ViewTtl, so the registry, the status endpoint and the health check share one read.
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
    KaleidoRegistryOptions options,
    TimeProvider time)
    : IKaleidoRegistrySource, IRegistryStatusReader, IDisposable
{
    internal static readonly TimeSpan ViewTtl = TimeSpan.FromSeconds(1);

    private readonly ConcurrentDictionary<string, KaleidoRegistry> _capabilities = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RegistryView? _view;

    // The store's registry is the whole registry: every participant registered its own capabilities.
    public bool IsFullRegistry => true;

    // The registry a consumer reads: every capability with its version's overall status.
    public async Task<KaleidoRegistry> GetAsync(int? maxParallelism, CancellationToken cancellationToken) =>
        (await ViewAsync(cancellationToken)).Contents;

    // The capabilities alone, for the hosts that keep their copy fresh: they change only with a deployment.
    public async Task<KaleidoRegistry> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
        (await ViewAsync(cancellationToken)).Capabilities;

    public async Task<KaleidoRegistryStatus> GetStatusAsync(CancellationToken cancellationToken) =>
        (await ViewAsync(cancellationToken)).Status;

    // Each version's overall availability, for the hosts keeping their copy fresh: no instances.
    public async Task<KaleidoRegistryAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
        (await ViewAsync(cancellationToken)).Availability;

    public Task RefreshAsync(CancellationToken cancellationToken)
    {
        _view = null;
        return Task.CompletedTask;
    }

    public void Dispose() => _gate.Dispose();

    private async Task<RegistryView> ViewAsync(CancellationToken cancellationToken)
    {
        if (Fresh(_view) is { } view)
        {
            return view;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return Fresh(_view) ?? (_view = await BuildAsync(cancellationToken));
        }
        finally
        {
            _gate.Release();
        }
    }

    private RegistryView? Fresh(RegistryView? view) =>
        view is not null && time.GetUtcNow() - view.BuiltUtc < ViewTtl ? view : null;

    private async Task<RegistryView> BuildAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IKaleidoRegistryStore>();
        var contents = await store.ReadAsync(cancellationToken);
        await LoadCapabilitiesAsync(store, contents, cancellationToken);

        var now = time.GetUtcNow();
        var (capabilities, statuses) = Build(contents, now);
        var status = new KaleidoRegistryStatus { Services = statuses, GeneratedAt = now, Revision = RevisionOf(statuses) };
        List<RegistryServiceAvailability> versions = [.. statuses.Select(x => new RegistryServiceAvailability { ServiceName = x.ServiceName, ServiceVersion = x.ServiceVersion, IsCurrent = x.IsCurrent, Address = x.Address, Status = x.Status })];
        var availability = new KaleidoRegistryAvailability
        {
            Services = versions,
            GeneratedAt = now,
            Revision = JsonSerializer.Serialize(versions, KaleidoJsonOptions.Options).ToRevision(),
        };

        return new RegistryView(
            capabilities with { Revision = capabilities.ToEntryJson().ToRevision() },
            capabilities.WithAvailability(availability),
            status,
            availability,
            now);
    }

    // The states and statuses, without the heartbeat times: a heartbeat alone changes nothing a reader acts on.
    private static string RevisionOf(List<RegistryServiceStatus> statuses) =>
        JsonSerializer.Serialize(
            statuses.Select(x => x with { Instances = [.. x.Instances.Select(i => i with { LastHeartbeatUtc = default })] }),
            KaleidoJsonOptions.Options).ToRevision();

    // Reads only the revisions not parsed yet, and forgets those the store no longer has.
    private async Task LoadCapabilitiesAsync(IKaleidoRegistryStore store, RegistryStoreContents contents, CancellationToken cancellationToken)
    {
        var stored = contents.Registrations.Select(x => x.Revision).ToHashSet(StringComparer.Ordinal);
        var missing = stored.Where(x => !_capabilities.ContainsKey(x)).ToList();

        if (missing.Count > 0)
        {
            foreach (var (revision, json) in await store.GetCapabilitiesAsync(missing, cancellationToken))
            {
                _capabilities[revision] = json.ToRegistryEntry();
            }
        }

        foreach (var gone in _capabilities.Keys.Where(x => !stored.Contains(x)).ToList())
        {
            _capabilities.TryRemove(gone, out _);
        }
    }

    private (KaleidoRegistry Registry, List<RegistryServiceStatus> Statuses) Build(RegistryStoreContents contents, DateTimeOffset now)
    {
        var versions = contents.Registrations.Select(x => (x.ServiceName, x.ServiceVersion))
            .Concat(contents.Instances.Select(x => (x.ServiceName, x.ServiceVersion)))
            .DistinctBy(x => $"{x.ServiceName.ToLowerInvariant()}/{x.ServiceVersion}", StringComparer.Ordinal)
            .OrderBy(x => x.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ServiceVersion, StringComparer.Ordinal)
            .ToList();

        var processes = new List<ProcessorRegistryResponse>();
        var queryables = new List<QueryableSourceResponse>();
        var statuses = new List<RegistryServiceStatus>();

        foreach (var (service, version) in versions)
        {
            var instances = contents.Instances
                .Where(x => Is(x.ServiceName, x.ServiceVersion, service, version))
                .Select(x => (Record: x, State: StateOf(x, now, contents.WatchResumedUtc)))
                .ToList();
            var live = instances.Where(x => x.State != RegistryInstanceState.NotReady).Select(x => x.Record).ToList();
            var published = Published(contents.Registrations.Where(x => Is(x.ServiceName, x.ServiceVersion, service, version)), live.Count > 0 ? live : [.. instances.Select(x => x.Record)]);

            // Each entry is the version it was registered under, whatever the stored copy says.
            processes.AddRange(published.Processes.Select(x => x with { ServiceVersion = version }));
            queryables.AddRange(published.Queryables.Select(x => x with { ServiceVersion = version }));
            statuses.Add(StatusOf(service, version, instances, contents.CurrentVersions));
        }

        return (new KaleidoRegistry { Processes = processes, Queryables = queryables, Missing = Missing(versions, processes) }, statuses);
    }

    // Unknown while not heard from since the watch resumed, for one lease after it; else by lease and health.
    private RegistryInstanceState StateOf(RegistryInstanceRecord instance, DateTimeOffset now, DateTimeOffset? resumed) =>
        resumed is { } since && instance.LastHeartbeatUtc < since && now < since + options.Lease
            ? RegistryInstanceState.Unknown
            : instance.Healthy && now - instance.LastHeartbeatUtc <= options.Lease
                ? RegistryInstanceState.Ready
                : RegistryInstanceState.NotReady;

    private static RegistryServiceStatus StatusOf(string service, string version, List<(RegistryInstanceRecord Record, RegistryInstanceState State)> instances, IReadOnlyDictionary<string, string> currentVersions)
    {
        var ready = instances.Count(x => x.State == RegistryInstanceState.Ready);
        var unknown = instances.Count(x => x.State == RegistryInstanceState.Unknown);

        return new RegistryServiceStatus
        {
            ServiceName = service,
            ServiceVersion = version,
            IsCurrent = currentVersions.TryGetValue(service, out var current) && string.Equals(current, version, StringComparison.Ordinal),
            Address = instances.OrderByDescending(x => x.Record.LastHeartbeatUtc).Select(x => x.Record.Address).FirstOrDefault(x => x is not null),
            Status = instances.Count == 0 ? RegistryCapabilityStatus.Unavailable
                : ready == instances.Count ? RegistryCapabilityStatus.Available
                : ready > 0 ? RegistryCapabilityStatus.Degraded
                : unknown > 0 ? RegistryCapabilityStatus.Unknown
                : RegistryCapabilityStatus.Unavailable,
            ReadyInstances = ready,
            UnknownInstances = unknown,
            Instances = [.. instances
                .OrderBy(x => x.Record.InstanceId)
                .Select(x => new RegistryInstanceStatus { InstanceId = x.Record.InstanceId, State = x.State, LastHeartbeatUtc = x.Record.LastHeartbeatUtc, Address = x.Record.Address })],
        };
    }

    // The registrations the instances run, oldest first; with no instance, every registration kept.
    private KaleidoRegistry Published(IEnumerable<RegistryRegistrationRecord> registrations, List<RegistryInstanceRecord> instances)
    {
        var running = instances.Select(x => x.Revision).ToHashSet(StringComparer.Ordinal);
        var all = registrations.OrderBy(x => x.RegisteredUtc).ToList();
        var used = all.Where(x => running.Contains(x.Revision)).ToList();

        return Shared([.. (used.Count > 0 ? used : all).Select(x => _capabilities.GetValueOrDefault(x.Revision)).OfType<KaleidoRegistry>()]);
    }

    // The rolling-deployment rule: a capability every revision has, as the oldest one defines it.
    private static KaleidoRegistry Shared(List<KaleidoRegistry> revisions)
    {
        if (revisions.Count <= 1)
        {
            return revisions.FirstOrDefault() ?? new KaleidoRegistry();
        }

        var oldest = revisions[0];

        var processes = oldest.Processes
            .Where(p => revisions.TrueForAll(r => r.Processes.Any(o => SameName(o.Name, p.Name))))
            .Select(p => p with
            {
                InitialSteps = [.. p.InitialSteps.Where(s => revisions.TrueForAll(r => ProcessorOf(r, p.Name).InitialSteps.Any(o => SameName(o.Name, s.Name))))],
                Steps = p.Steps is null ? null : [.. p.Steps.Where(s => revisions.TrueForAll(r => (ProcessorOf(r, p.Name).Steps ?? []).Any(o => SameName(o.Name, s.Name))))],
            });

        var queryables = oldest.Queryables
            .Where(q => revisions.TrueForAll(r => r.Queryables.Any(o => SameName(o.Name, q.Name))))
            .Select(q => q with
            {
                Views = [.. q.Views.Where(v => revisions.TrueForAll(r => SourceOf(r, q.Name).Views.Any(o => SameName(o.Name, v.Name))))],
            });

        return new KaleidoRegistry { Processes = [.. processes], Queryables = [.. queryables] };
    }

    private List<RegistryMissingService> Missing(List<(string ServiceName, string ServiceVersion)> versions, List<ProcessorRegistryResponse> processes)
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

    private static ProcessorRegistryResponse ProcessorOf(KaleidoRegistry capabilities, string name) =>
        capabilities.Processes.First(x => SameName(x.Name, name));

    private static QueryableSourceResponse SourceOf(KaleidoRegistry capabilities, string name) =>
        capabilities.Queryables.First(x => SameName(x.Name, name));

    private static bool Is(string service, string version, string otherService, string otherVersion) =>
        SameName(service, otherService) && string.Equals(version, otherVersion, StringComparison.Ordinal);

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private sealed record RegistryView(KaleidoRegistry Capabilities, KaleidoRegistry Contents, KaleidoRegistryStatus Status, KaleidoRegistryAvailability Availability, DateTimeOffset BuiltUtc);
}

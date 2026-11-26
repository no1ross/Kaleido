using Microsoft.Extensions.DependencyInjection;
using static Kaleido.Registry.Server.UnitTests.RegistryTestData;

namespace Kaleido.Registry.Server.UnitTests;

// #553: the control registry's full registry from its store - capabilities per service version (the
// rolling rule), each version's status and what is missing. The cache collaborator owns freshness.
public sealed class StoreRegistrySourceTests
    : SutFixture
{
    private static readonly KaleidoRegistryOptions Defaults = new();
    private static readonly DateTimeOffset Now = Start.AddHours(1);

    private readonly Mock<IKaleidoRegistryStore> store = new();
    private readonly Mock<IRegistryCache> cache = new();
    private readonly Dictionary<(string Service, string Version, string Build), KaleidoRegistry> capabilities = [];
    private readonly FakeTimeProvider time = new(Now);
    private RegistryStoreContents contents = new();

    public StoreRegistrySourceTests()
    {
        store.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => contents);
        store.Setup(x => x.GetCapabilitiesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string service, string version, IReadOnlyCollection<string> builds, CancellationToken _) =>
                builds.Where(build => capabilities.ContainsKey((service, version, build)))
                    .ToDictionary(build => build, build => capabilities[(service, version, build)], StringComparer.Ordinal));
        cache.Setup(x => x.GetViewAsync(It.IsAny<Func<CancellationToken, Task<RegistryView>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<RegistryView>> build, CancellationToken token) => build(token));
        cache.Setup(x => x.GetCapabilitiesAsync(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task<KaleidoRegistry>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<KaleidoRegistry>> load, CancellationToken token) => load(token));
    }

    private StoreRegistrySource CreateSut(KaleidoRegistryOptions? options = null)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IKaleidoRegistryStore))).Returns(store.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return new StoreRegistrySource(factory.Object, cache.Object, options ?? Defaults, time);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private RegistryRegistrationRecord Stored(string service, string version, KaleidoRegistry published, DateTimeOffset? registered = null)
    {
        var build = $"1.0.0-preview.{capabilities.Count + 1}";
        var (record, model) = Registration(service, version, published, registered, build);
        capabilities[(service, version, build)] = model;
        return record;
    }

    // The registry and its statuses, as one source built them.
    private sealed record View(KaleidoRegistry Registry, KaleidoRegistryStatus Status)
    {
        public IReadOnlyCollection<ProcessRegistryItem> Processes => Registry.Processes;

        public IReadOnlyCollection<QueryableRegistryItem> Queryables => Registry.Queryables;

        public IReadOnlyCollection<RegistryMissingService> Missing => Registry.Missing;

        public IReadOnlyCollection<RegistryReadError> Errors => Registry.Errors;

        public IReadOnlyCollection<RegistryServiceStatus> Services => Status.Services;
    }

    private async Task<View> ViewAsync(IEnumerable<RegistryRegistrationRecord> registrations, IEnumerable<RegistryInstanceRecord> instances, KaleidoRegistryOptions? options = null, params (string Service, string Version)[] current)
    {
        contents = new()
        {
            Registrations = [.. registrations],
            Instances = [.. instances],
            CurrentVersions = current.ToDictionary(x => x.Service, x => x.Version, StringComparer.OrdinalIgnoreCase),
        };

        var sut = CreateSut(options);

        return new View(await sut.GetAsync(Cancel), await sut.GetStatusAsync(Cancel));
    }

    [Fact]
    public async Task AReadyInstance_PublishesItsVersionsCapabilities_AndItsStatus()
    {
        var registration = Stored("radiology", "2026-11", Entry([ProcessorWith("radiology", "OrderScanStep")], [SourceWith("radiology", "ScanSource")]));

        var instance = Instance(registration, Now);

        var view = await ViewAsync([registration], [instance], current: ("radiology", "2026-11"));

        Assert.Equal(["OrderScanStep"], Assert.Single(view.Processes).Steps!.Select(x => x.Name));
        Assert.Equal("ScanSource", Assert.Single(view.Queryables).Name);
        Assert.Equal(new RegistryTransportRoute { Transport = "http", GatewayRoute = "/radiology/2026-11" }, Assert.Single(Assert.Single(view.Registry.Routes).Transports));
        var status = Assert.Single(view.Services);
        Assert.Equal(
            new RegistryServiceStatus { ServiceName = "radiology", ServiceVersion = "2026-11", IsCurrent = true, Status = RegistryStatusNames.Available, Transports = status.Transports, ReadyInstances = 1, Instances = status.Instances },
            status);
        Assert.Equal(
            new RegistryInstanceStatus { InstanceId = instance.InstanceId, State = RegistryInstanceStateNames.Ready, LastHeartbeatUtc = Now },
            Assert.Single(status.Instances));
        Assert.Empty(view.Missing);
        Assert.Empty(view.Errors);
    }

    // The lease: ready while the last heartbeat is within MissedHeartbeats x HeartbeatInterval.
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public async Task AnInstance_IsReadyAroundTheEndOfItsLease(long ticksPastTheLease, int ready)
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var heartbeat = Now - Defaults.Lease - TimeSpan.FromTicks(ticksPastTheLease);

        var status = Assert.Single((await ViewAsync([registration], [Instance(registration, heartbeat)])).Services);

        Assert.Equal(ready, status.ReadyInstances);
        Assert.Equal(ready == 1 ? RegistryInstanceStateNames.Ready : RegistryInstanceStateNames.NotReady, Assert.Single(status.Instances).State);
    }

    // A version with no ready instance stays listed, flagged unavailable.
    [Fact]
    public async Task AVersionWithNoReadyInstance_KeepsItsCapabilities_Unavailable()
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));

        var view = await ViewAsync([registration], [Instance(registration, Now, healthy: false)]);

        Assert.Single(view.Processes);
        Assert.Equal(RegistryStatusNames.Unavailable, Assert.Single(view.Services).Status);
    }

    [Fact]
    public async Task RegistrationsNoInstanceRuns_StayListed_Unavailable()
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));

        var view = await ViewAsync([registration], []);

        Assert.Single(view.Processes);
        var status = Assert.Single(view.Services);
        Assert.Empty(status.Instances);
        Assert.Equal(RegistryStatusNames.Unavailable, status.Status);
    }

    // The rolling-deployment rule: only what every ready instance's revision has, as the oldest defines it.
    [Fact]
    public async Task DuringARollingDeployment_OnlyTheSharedCapabilities_ArePublished_AsTheOldestRevisionDefinesThem()
    {
        var old = Stored("radiology", "1", Entry(
            [ProcessorWith("radiology", "OrderScanStep", "CancelStep") with { Description = "old" }],
            [SourceWith("radiology", "ScanSource", "ScanCard", "OldView"), SourceWith("radiology", "Retired")]));
        var rolled = Stored("radiology", "1", Entry(
            [ProcessorWith("radiology", "OrderScanStep", "ReviewStep") with { Description = "new" }],
            [SourceWith("radiology", "ScanSource", "ScanCard"), SourceWith("radiology", "Added")]), Start.AddMinutes(5));

        var view = await ViewAsync([rolled, old], [Instance(old, Now), Instance(rolled, Now)]);

        var processor = Assert.Single(view.Processes);
        Assert.Equal("old", processor.Description);
        Assert.Equal(["OrderScanStep"], processor.Steps!.Select(x => x.Name));
        Assert.Equal(["OrderScanStep"], processor.InitialSteps.Select(x => x.Name));
        var source = Assert.Single(view.Queryables);
        Assert.Equal("ScanSource", source.Name);
        Assert.Equal(["ScanCard"], source.Views.Select(x => x.Name));
    }

    // A revision that only a not-ready instance runs does not narrow what the ready ones publish.
    [Fact]
    public async Task ARevisionNoReadyInstanceRuns_DoesNotNarrowThePublication()
    {
        var old = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep", "CancelStep")], []));
        var failed = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []), Start.AddMinutes(5));

        var view = await ViewAsync([old, failed], [Instance(old, Now), Instance(failed, Now, healthy: false)]);

        Assert.Equal(["CancelStep", "OrderScanStep"], Assert.Single(view.Processes).Steps!.Select(x => x.Name).Order(StringComparer.Ordinal));
    }

    // Versions are kept apart; only the pointer says which is current.
    [Fact]
    public async Task TwoVersions_AreEachListed_AndOnlyTheCurrentOneIsMarked()
    {
        var v1 = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var v2 = Stored("radiology", "2", Entry([ProcessorWith("radiology", "OrderScanStep", "ReviewStep")], []));

        var view = await ViewAsync([v1, v2], [Instance(v1, Now), Instance(v2, Now)], current: ("radiology", "1"));

        Assert.Equal(2, view.Processes.Count);
        Assert.Equal([("1", true), ("2", false)], view.Services.Select(x => (x.ServiceVersion, x.IsCurrent)));
    }

    // Missing: an expected processor version, or a service a published step looks up, with no registration.
    [Fact]
    public async Task WhatIsExpectedOrLookedUp_AndNotRegistered_IsMissing()
    {
        var options = Defaults with { Expected = [new KaleidoRegistryExpectedService { ServiceName = "radiology", Versions = ["1", "2"] }] };
        var step = new ProcessStepResponse
        {
            Name = "OrderScanStep",
            Fields =
            [
                new ProcessFieldMetadata { Name = "MemberId", DataType = new DataTypeMetadata("string"), Lookup = new ProcessFieldLookup { Service = "member", Source = "MemberSource", Field = "Id", Group = "MemberId" } },
                new ProcessFieldMetadata { Name = "ScanId", DataType = new DataTypeMetadata("string"), Lookup = new ProcessFieldLookup { Service = "radiology", Source = "ScanSource", Field = "Id", Group = "ScanId" } },
            ],
        };
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology") with { Steps = [step] }], []));

        var view = await ViewAsync([registration], [Instance(registration, Now)], options);

        Assert.Equal(
            [new RegistryMissingService { ServiceName = "radiology", ServiceVersion = "2", Reason = "expected" }, new RegistryMissingService { ServiceName = "member", Reason = "lookup" }],
            view.Missing);
    }

    [Fact]
    public async Task EachRevision_IsRequestedByItsCacheKey_EvenAfterARollback()
    {
        var sut = CreateSut();
        var v1 = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var v2 = Stored("radiology", "2", Entry([ProcessorWith("radiology", "ReviewStep")], []));
        contents = new() { Registrations = [v1], Instances = [Instance(v1, Now)] };
        await sut.GetAsync(Cancel);

        time.Advance(Defaults.Cache.ViewTtl);
        contents = new() { Registrations = [v1, v2], Instances = [Instance(v1, Now), Instance(v2, Now)] };
        await sut.GetAsync(Cancel);

        time.Advance(Defaults.Cache.ViewTtl);
        contents = new() { Registrations = [v2], Instances = [Instance(v2, Now)] };
        await sut.GetAsync(Cancel);

        time.Advance(Defaults.Cache.ViewTtl);
        contents = new() { Registrations = [v1, v2], Instances = [Instance(v1, Now), Instance(v2, Now)] };
        var view = await sut.GetAsync(Cancel);

        cache.Verify(x => x.GetCapabilitiesAsync($"{v1.ServiceName}@{v1.ServiceVersion}@{v1.BuildVersion}", It.IsAny<Func<CancellationToken, Task<KaleidoRegistry>>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        cache.Verify(x => x.GetCapabilitiesAsync($"{v2.ServiceName}@{v2.ServiceVersion}@{v2.BuildVersion}", It.IsAny<Func<CancellationToken, Task<KaleidoRegistry>>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.Equal(2, view.Processes.Count);
    }

    [Fact]
    public async Task ARevisionRemovedBetweenTheStoreReads_IsNotCachedOrPublished()
    {
        var sut = CreateSut();
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        contents = new() { Registrations = [registration], Instances = [Instance(registration, Now)] };
        capabilities.Remove((registration.ServiceName, registration.ServiceVersion, registration.BuildVersion));

        var registry = await sut.GetAsync(Cancel);

        Assert.Empty(registry.Processes);
        cache.Verify(x => x.GetCapabilitiesAsync($"{registration.ServiceName}@{registration.ServiceVersion}@{registration.BuildVersion}", It.IsAny<Func<CancellationToken, Task<KaleidoRegistry>>>(), Cancel), Times.Once);
    }

    [Fact]
    public async Task Refresh_InvalidatesTheMergedView()
    {
        var sut = CreateSut();

        await sut.RefreshAsync(Cancel);

        cache.Verify(x => x.InvalidateViewAsync(Cancel), Times.Once);
        store.Verify(x => x.ReadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // The statuses have their own snapshot version: the transport exposes it as a conditional-read token.
    [Fact]
    public async Task TheStatus_TracksInstancesWithoutComputingAnHttpRevision()
    {
        var sut = CreateSut();
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var instance = Instance(registration, Now);
        contents = new() { Registrations = [registration], Instances = [instance] };

        var first = await sut.GetStatusAsync(Cancel);
        time.Advance(Defaults.Cache.ViewTtl);
        var same = await sut.GetStatusAsync(Cancel);
        time.Advance(Defaults.Cache.ViewTtl);
        contents = contents with { Instances = [instance, Instance(registration, Now)] };
        var changed = await sut.GetStatusAsync(Cancel);

        Assert.Equal((1, time.GetUtcNow() - (Defaults.Cache.ViewTtl * 2)), (Assert.Single(first.Services).Instances.Count, first.GeneratedAt));
        Assert.Single(Assert.Single(same.Services).Instances);
        Assert.Equal(2, Assert.Single(changed.Services).Instances.Count);
        Assert.NotNull(first.Revision);
        Assert.Equal(first.Revision, same.Revision);
        Assert.NotEqual(first.Revision, changed.Revision);
    }

    [Fact]
    public async Task AnEmptyStore_PublishesNothing()
    {
        var view = await ViewAsync([], []);

        Assert.Empty(view.Processes);
        Assert.Empty(view.Services);
        store.Verify(x => x.GetCapabilitiesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // A heartbeat changes its reported time without changing a healthy instance's state.
    [Fact]
    public async Task TheStatus_ReportsHeartbeatTimesWithoutChangingInstanceState()
    {
        var sut = CreateSut();
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var instance = Instance(registration, Now);
        contents = new() { Registrations = [registration], Instances = [instance] };

        var first = await sut.GetStatusAsync(Cancel);
        time.Advance(Defaults.Cache.ViewTtl);
        contents = contents with { Instances = [instance with { LastHeartbeatUtc = Now + Defaults.Cache.ViewTtl }] };
        var later = await sut.GetStatusAsync(Cancel);

        Assert.Equal(Assert.Single(Assert.Single(first.Services).Instances).State, Assert.Single(Assert.Single(later.Services).Instances).State);
        Assert.Equal(Now + Defaults.Cache.ViewTtl, Assert.Single(Assert.Single(later.Services).Instances).LastHeartbeatUtc);
    }

    // The overall status of a version, from its instances' states.
    [Theory]
    [InlineData(2, 0, 0, RegistryStatusNames.Available)]
    [InlineData(1, 1, 0, RegistryStatusNames.Degraded)]
    [InlineData(1, 0, 1, RegistryStatusNames.Degraded)]
    [InlineData(0, 1, 1, RegistryStatusNames.Unknown)]
    [InlineData(0, 2, 0, RegistryStatusNames.Unavailable)]
    public async Task AVersionsStatus_FollowsItsInstances(int ready, int notReady, int unknown, string expected)
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var resumed = Now - TimeSpan.FromSeconds(5);
        var instances = Enumerable.Range(0, ready).Select(_ => Instance(registration, Now))
            .Concat(Enumerable.Range(0, notReady).Select(_ => Instance(registration, Now, healthy: false)))
            .Concat(Enumerable.Range(0, unknown).Select(_ => Instance(registration, resumed - TimeSpan.FromMinutes(5))));
        contents = new() { Registrations = [registration], Instances = [.. instances], WatchResumedUtc = resumed };

        var status = Assert.Single((await CreateSut().GetStatusAsync(Cancel)).Services);

        Assert.Equal((expected, ready, unknown), (status.Status, status.ReadyInstances, status.UnknownInstances));
    }

    // After every registry node was down: an instance last heard from before the watch resumed is Unknown
    // for one lease after it; one heard from since is judged as usual.
    [Theory]
    [InlineData(-1, RegistryInstanceStateNames.Unknown)]
    [InlineData(0, RegistryInstanceStateNames.NotReady)]
    public async Task AnInstanceNotHeardFromSinceTheWatchResumed_IsUnknown_ForOneLease(long ticksFromTheLease, string expected)
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var resumed = Now - Defaults.Lease - TimeSpan.FromTicks(ticksFromTheLease);
        var silent = Instance(registration, resumed - TimeSpan.FromTicks(1));
        var reported = Instance(registration, Now);
        contents = new() { Registrations = [registration], Instances = [silent, reported], WatchResumedUtc = resumed };

        var states = Assert.Single((await CreateSut().GetStatusAsync(Cancel)).Services).Instances.ToDictionary(x => x.InstanceId, x => x.State);

        Assert.Equal(expected, states[silent.InstanceId]);
        Assert.Equal(RegistryInstanceStateNames.Ready, states[reported.InstanceId]);
    }

    // An Unknown instance's revision is believed running: it takes part in the rolling rule.
    [Fact]
    public async Task AnUnknownInstance_TakesPartInTheRollingRule()
    {
        var old = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep", "CancelStep")], []));
        var rolled = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []), Start.AddMinutes(5));
        var resumed = Now - TimeSpan.FromSeconds(5);
        contents = new() { Registrations = [old, rolled], Instances = [Instance(old, Now), Instance(rolled, resumed - TimeSpan.FromMinutes(1))], WatchResumedUtc = resumed };

        var registry = await CreateSut().GetAsync(Cancel);

        Assert.Equal(["OrderScanStep"], Assert.Single(registry.Processes).Steps!.Select(x => x.Name));
    }

    // The registry a consumer reads carries each capability's status; the capabilities the hosts sync do not,
    // so their revision changes only with a deployment.
    [Fact]
    public async Task TheRegistry_CarriesEachCapabilitysStatus_AndTheCapabilitiesDoNot()
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], [SourceWith("radiology", "ScanSource")]));
        var sut = CreateSut();
        contents = new() { Registrations = [registration], Instances = [Instance(registration, Now)] };

        var registry = await sut.GetAsync(Cancel);
        var capabilities = await sut.GetCapabilitiesAsync(Cancel);
        time.Advance(Defaults.Cache.ViewTtl);
        contents = contents with { Instances = [Instance(registration, Now, healthy: false)] };
        var afterAFailure = await sut.GetCapabilitiesAsync(Cancel);

        Assert.Equal(RegistryStatusNames.Available, Assert.Single(registry.Processes).Status);
        Assert.Equal(RegistryStatusNames.Available, Assert.Single(registry.Queryables).Status);
        Assert.Equal(RegistryStatusNames.NotReported, Assert.Single(capabilities.Processes).Status);
        Assert.Equal(RegistryStatusNames.NotReported, Assert.Single(afterAFailure.Processes).Status);
        Assert.Equal(capabilities.Processes.Select(x => x.Name), afterAFailure.Processes.Select(x => x.Name));
        Assert.NotNull(capabilities.Revision);
        Assert.Equal(capabilities.Revision, afterAFailure.Revision);
    }

    [Fact]
    public async Task RollingDeployment_AdvertisesOnlyTransportBindingsEveryLiveRevisionSupports()
    {
        var sut = CreateSut();
        var older = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []) with
        {
            Routes = [new RegistryServiceRoute { ServiceName = "radiology", ServiceVersion = "1", Transports = [new RegistryTransportRoute { Transport = "http", GatewayRoute = "/radiology/1" }] }],
        }, registered: Start);
        var newer = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []) with
        {
            Routes = [new RegistryServiceRoute { ServiceName = "radiology", ServiceVersion = "1", Transports =
            [
                new RegistryTransportRoute { Transport = "http", GatewayRoute = "/radiology/1" },
                new RegistryTransportRoute { Transport = "grpc", GatewayRoute = "/grpc/radiology/1" },
            ] }],
        }, registered: Start.AddMinutes(1));
        contents = new() { Registrations = [newer, older], Instances = [Instance(older, Now), Instance(newer, Now)] };

        var duringRollout = await sut.GetAvailabilityAsync(Cancel);
        time.Advance(Defaults.Cache.ViewTtl);
        contents = contents with { Instances = [Instance(newer, time.GetUtcNow())] };
        var completedRollout = await sut.GetAvailabilityAsync(Cancel);

        Assert.Equal(["http"], Assert.Single(duringRollout.Services).Transports.Select(x => x.Transport));
        Assert.Equal(["http", "grpc"], Assert.Single(completedRollout.Services).Transports.Select(x => x.Transport));
        Assert.NotNull(duringRollout.Revision);
        Assert.NotEqual(duringRollout.Revision, completedRollout.Revision);
    }

    [Fact]
    public async Task ATransportRouteChange_AppearsInTheAvailabilityStatusAndRegistry()
    {
        var sut = CreateSut();
        var first = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []) with
        {
            Routes = [new RegistryServiceRoute { ServiceName = "radiology", ServiceVersion = "1", Transports = [new RegistryTransportRoute { Transport = "http", GatewayRoute = "/radiology/1" }] }],
        });
        contents = new() { Registrations = [first], Instances = [Instance(first, Now)] };
        var before = await sut.GetAvailabilityAsync(Cancel);
        var beforeStatus = await sut.GetStatusAsync(Cancel);

        time.Advance(Defaults.Cache.ViewTtl);
        var next = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []) with
        {
            Routes = [new RegistryServiceRoute { ServiceName = "radiology", ServiceVersion = "1", Transports =
            [
                new RegistryTransportRoute { Transport = "http", GatewayRoute = "/radiology/1" },
                new RegistryTransportRoute { Transport = "grpc", GatewayRoute = "/grpc/radiology/1" },
            ] }],
        });
        contents = contents with { Registrations = [next], Instances = [Instance(next, time.GetUtcNow())] };
        var after = await sut.GetAvailabilityAsync(Cancel);
        var afterStatus = await sut.GetStatusAsync(Cancel);
        var registry = await sut.GetAsync(Cancel);

        Assert.Equal(["http"], Assert.Single(before.Services).Transports.Select(x => x.Transport));
        Assert.Equal(["http"], Assert.Single(beforeStatus.Services).Transports.Select(x => x.Transport));
        Assert.Equal(["http", "grpc"], Assert.Single(after.Services).Transports.Select(x => x.Transport));
        Assert.Equal(["http", "grpc"], Assert.Single(afterStatus.Services).Transports.Select(x => x.Transport));
        Assert.Equal(["http", "grpc"], Assert.Single(registry.Routes).Transports.Select(x => x.Transport));
        Assert.NotEqual(before.CapabilitiesRevision, after.CapabilitiesRevision);
        Assert.NotEqual(before.Revision, after.Revision);
        Assert.NotEqual(beforeStatus.Revision, afterStatus.Revision);
    }

    // The availability a host keeps fresh: each version's overall status, current version and address, no
    // instances. An instance joining within the same status preserves availability; a status flip does not.
    [Fact]
    public async Task TheAvailability_TracksVersionStatusNotInstanceCount()
    {
        var sut = CreateSut();
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        contents = new() { Registrations = [registration], Instances = [Instance(registration, Now)], CurrentVersions = new Dictionary<string, string> { ["radiology"] = "1" } };

        var first = await sut.GetAvailabilityAsync(Cancel);
        time.Advance(Defaults.Cache.ViewTtl);
        contents = contents with { Instances = [.. contents.Instances, Instance(registration, Now)] };
        var anotherInstance = await sut.GetAvailabilityAsync(Cancel);
        time.Advance(Defaults.Cache.ViewTtl);
        contents = contents with { Instances = [.. contents.Instances, Instance(registration, Now, healthy: false)] };
        var degraded = await sut.GetAvailabilityAsync(Cancel);

        Assert.Equal(
            new RegistryServiceAvailability { ServiceName = "radiology", ServiceVersion = "1", IsCurrent = true, Status = RegistryStatusNames.Available, Transports = Assert.Single(first.Services).Transports },
            Assert.Single(first.Services));
        Assert.Equal(Assert.Single(first.Services).Status, Assert.Single(anotherInstance.Services).Status);
        Assert.Equal(RegistryStatusNames.Degraded, Assert.Single(degraded.Services).Status);
        Assert.Equal(first.Revision, anotherInstance.Revision);
        Assert.NotEqual(first.Revision, degraded.Revision);
    }

    // The registry sets the hosts' pace and exposes new build capabilities; the transport supplies
    // conditional-read tokens when it serves the availability and capability documents.
    [Fact]
    public async Task TheAvailability_CarriesThePaceWhileNewBuildCapabilitiesArePublished()
    {
        var sut = CreateSut();
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        contents = new() { Registrations = [registration], Instances = [Instance(registration, Now)] };

        var standard = await sut.GetAvailabilityAsync(Cancel);
        var slower = await CreateSut(Defaults with { HeartbeatInterval = TimeSpan.FromSeconds(20) }).GetAvailabilityAsync(Cancel);
        time.Advance(Defaults.Cache.ViewTtl);
        // A new build of the same version: the versions and their statuses are as they were, the capabilities are not.
        var rebuilt = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep", "CancelScanStep")], []));
        contents = contents with { Registrations = [rebuilt], Instances = [Instance(rebuilt, Now)] };
        var afterDeployment = await sut.GetAvailabilityAsync(Cancel);

        Assert.Equal(Defaults.HeartbeatInterval, standard.AvailabilityInterval);
        Assert.Equal(TimeSpan.FromSeconds(20), slower.AvailabilityInterval);
        Assert.Equal(["OrderScanStep", "CancelScanStep"], Assert.Single((await sut.GetCapabilitiesAsync(Cancel)).Processes).Steps!.Select(x => x.Name));
        Assert.Equal((await sut.GetCapabilitiesAsync(Cancel)).Revision, afterDeployment.CapabilitiesRevision);
        Assert.NotEqual(standard.CapabilitiesRevision, afterDeployment.CapabilitiesRevision);
        Assert.NotEqual(standard.Revision, slower.Revision);
        Assert.Equal(standard.Services.Select(x => (x.ServiceName, x.ServiceVersion, x.IsCurrent, x.Status)), afterDeployment.Services.Select(x => (x.ServiceName, x.ServiceVersion, x.IsCurrent, x.Status)));
        Assert.Equal<RegistryTransportRoute>(Assert.Single(standard.Services).Transports, Assert.Single(afterDeployment.Services).Transports);
    }

    // The composed registry carries its availability and its own snapshot version for all transports.
    [Fact]
    public async Task TheRegistry_ComposesCapabilityStatusWithASharedSnapshotVersion()
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        contents = new() { Registrations = [registration], Instances = [Instance(registration, Now)] };

        var registry = await CreateSut().GetAsync(Cancel);

        Assert.Equal(RegistryStatusNames.Available, Assert.Single(registry.Processes).Status);
        Assert.NotNull(registry.Revision);
    }
}

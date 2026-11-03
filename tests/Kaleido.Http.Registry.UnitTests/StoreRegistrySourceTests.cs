using Microsoft.Extensions.DependencyInjection;
using static Kaleido.Http.Registry.UnitTests.RegistryTestData;

namespace Kaleido.Http.Registry.UnitTests;

// #553: the control registry's full registry from its store - capabilities per service version (the
// rolling rule), each version's status, what is missing - and how it reads the store: each revision's
// capabilities once, and one view shared for a second.
public sealed class StoreRegistrySourceTests
    : SutFixture
{
    private static readonly KaleidoRegistryOptions Defaults = new();
    private static readonly DateTimeOffset Now = Start.AddHours(1);

    private readonly Mock<IKaleidoRegistryStore> store = new();
    private readonly Dictionary<string, string> capabilities = new(StringComparer.Ordinal);
    private readonly FakeTimeProvider time = new(Now);
    private RegistryStoreContents contents = new();

    public StoreRegistrySourceTests()
    {
        store.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => contents);
        store.Setup(x => x.GetCapabilitiesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> revisions, CancellationToken _) => revisions.Where(capabilities.ContainsKey).ToDictionary(x => x, x => capabilities[x], StringComparer.Ordinal));
    }

    private StoreRegistrySource CreateSut(KaleidoRegistryOptions? options = null)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IKaleidoRegistryStore))).Returns(store.Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return new StoreRegistrySource(factory.Object, options ?? Defaults, time);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private RegistryRegistrationRecord Stored(string service, string version, KaleidoRegistry published, DateTimeOffset? registered = null)
    {
        var (record, json) = Registration(service, version, published, registered);
        capabilities[record.Revision] = json;
        return record;
    }

    // The registry and its statuses, as one source built them.
    private sealed record View(KaleidoRegistry Registry, KaleidoRegistryStatus Status)
    {
        public IReadOnlyCollection<ProcessorRegistryResponse> Processes => Registry.Processes;

        public IReadOnlyCollection<QueryableSourceResponse> Queryables => Registry.Queryables;

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

        return new View(await sut.GetAsync(maxParallelism: null, Cancel), await sut.GetStatusAsync(Cancel));
    }

    [Fact]
    public async Task AReadyInstance_PublishesItsVersionsCapabilities_AndItsStatus()
    {
        var registration = Stored("radiology", "2026-11", Entry([ProcessorWith("radiology", "OrderScanStep")], [SourceWith("radiology", "ScanSource")]));

        var instance = Instance(registration, Now, address: "https://radiology");

        var view = await ViewAsync([registration], [instance], current: ("radiology", "2026-11"));

        Assert.Equal(["OrderScanStep"], Assert.Single(view.Processes).Steps!.Select(x => x.Name));
        Assert.Equal("ScanSource", Assert.Single(view.Queryables).Name);
        var status = Assert.Single(view.Services);
        Assert.Equal(
            new RegistryServiceStatus { ServiceName = "radiology", ServiceVersion = "2026-11", IsCurrent = true, Address = "https://radiology", Status = RegistryCapabilityStatus.Available, ReadyInstances = 1, Instances = status.Instances },
            status);
        Assert.Equal(
            new RegistryInstanceStatus { InstanceId = instance.InstanceId, State = RegistryInstanceState.Ready, LastHeartbeatUtc = Now, Address = "https://radiology" },
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
        Assert.Equal(ready == 1 ? RegistryInstanceState.Ready : RegistryInstanceState.NotReady, Assert.Single(status.Instances).State);
    }

    // A version with no ready instance stays listed, flagged unavailable.
    [Fact]
    public async Task AVersionWithNoReadyInstance_KeepsItsCapabilities_Unavailable()
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));

        var view = await ViewAsync([registration], [Instance(registration, Now, healthy: false)]);

        Assert.Single(view.Processes);
        Assert.Equal(RegistryCapabilityStatus.Unavailable, Assert.Single(view.Services).Status);
    }

    [Fact]
    public async Task RegistrationsNoInstanceRuns_StayListed_Unavailable()
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));

        var view = await ViewAsync([registration], []);

        Assert.Single(view.Processes);
        var status = Assert.Single(view.Services);
        Assert.Empty(status.Instances);
        Assert.Equal(RegistryCapabilityStatus.Unavailable, status.Status);
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

    // The address the instance seen most recently registered.
    [Fact]
    public async Task TheAddress_IsTheOneTheLatestInstanceRegistered()
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));

        var view = await ViewAsync([registration], [Instance(registration, Now.AddSeconds(-5), address: "https://old"), Instance(registration, Now, address: "https://new")]);

        Assert.Equal("https://new", Assert.Single(view.Services).Address);
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

    // A revision never changes: its capabilities are read once, and forgotten when the store drops it.
    [Fact]
    public async Task EachRevisionsCapabilities_AreReadOnce_AndForgottenWhenGone()
    {
        var sut = CreateSut();
        var v1 = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var v2 = Stored("radiology", "2", Entry([ProcessorWith("radiology", "ReviewStep")], []));
        contents = new() { Registrations = [v1], Instances = [Instance(v1, Now)] };
        await sut.GetAsync(null, Cancel);

        time.Advance(StoreRegistrySource.ViewTtl);
        contents = new() { Registrations = [v1, v2], Instances = [Instance(v1, Now), Instance(v2, Now)] };
        await sut.GetAsync(null, Cancel);

        time.Advance(StoreRegistrySource.ViewTtl);
        contents = new() { Registrations = [v2], Instances = [Instance(v2, Now)] };
        await sut.GetAsync(null, Cancel);

        time.Advance(StoreRegistrySource.ViewTtl);
        contents = new() { Registrations = [v1, v2], Instances = [Instance(v1, Now), Instance(v2, Now)] };
        var view = await sut.GetAsync(null, Cancel);

        store.Verify(x => x.GetCapabilitiesAsync(It.Is<IReadOnlyCollection<string>>(r => r.SequenceEqual(new[] { v1.Revision })), It.IsAny<CancellationToken>()), Times.Exactly(2));
        store.Verify(x => x.GetCapabilitiesAsync(It.Is<IReadOnlyCollection<string>>(r => r.SequenceEqual(new[] { v2.Revision })), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, view.Processes.Count);
    }

    // One view serves every read for ViewTtl; a refresh drops it.
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    public async Task TheView_IsSharedForItsTtl(long ticksFromTheTtl, int reads)
    {
        var sut = CreateSut();

        await sut.GetAsync(null, Cancel);
        time.Advance(StoreRegistrySource.ViewTtl + TimeSpan.FromTicks(ticksFromTheTtl));
        await sut.GetStatusAsync(Cancel);

        store.Verify(x => x.ReadAsync(It.IsAny<CancellationToken>()), Times.Exactly(reads));
    }

    [Fact]
    public async Task Refresh_DropsTheView()
    {
        var sut = CreateSut();
        await sut.GetAsync(null, Cancel);

        await sut.RefreshAsync(Cancel);
        await sut.GetAsync(null, Cancel);

        store.Verify(x => x.ReadAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // The statuses, apart: their revision follows their states only.
    [Fact]
    public async Task TheStatus_HasItsOwnRevision_FollowingTheStatusesOnly()
    {
        var sut = CreateSut();
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var instance = Instance(registration, Now);
        contents = new() { Registrations = [registration], Instances = [instance] };

        var first = await sut.GetStatusAsync(Cancel);
        time.Advance(StoreRegistrySource.ViewTtl);
        var same = await sut.GetStatusAsync(Cancel);
        time.Advance(StoreRegistrySource.ViewTtl);
        contents = contents with { Instances = [instance, Instance(registration, Now)] };
        var changed = await sut.GetStatusAsync(Cancel);

        Assert.Equal((1, time.GetUtcNow() - (StoreRegistrySource.ViewTtl * 2)), (Assert.Single(first.Services).Instances.Count, first.GeneratedAt));
        Assert.Equal(first.Revision, same.Revision);
        Assert.NotEqual(first.Revision, changed.Revision);
        Assert.Matches("^[0-9a-f]{64}$", first.Revision!);
    }

    [Fact]
    public async Task AnEmptyStore_PublishesNothing()
    {
        var view = await ViewAsync([], []);

        Assert.Empty(view.Processes);
        Assert.Empty(view.Services);
        store.Verify(x => x.GetCapabilitiesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The store's registry is the whole registry: every participant registered its own capabilities.
    [Fact]
    public void TheStoresRegistry_IsTheFullRegistry()
    {
        Assert.True(CreateSut().IsFullRegistry);
    }

    // A heartbeat alone (a later time, the same state) does not change the status revision.
    [Fact]
    public async Task TheStatusRevision_IgnoresHeartbeatTimes()
    {
        var sut = CreateSut();
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var instance = Instance(registration, Now);
        contents = new() { Registrations = [registration], Instances = [instance] };

        var first = await sut.GetStatusAsync(Cancel);
        time.Advance(StoreRegistrySource.ViewTtl);
        contents = contents with { Instances = [instance with { LastHeartbeatUtc = Now + StoreRegistrySource.ViewTtl }] };
        var later = await sut.GetStatusAsync(Cancel);

        Assert.Equal(first.Revision, later.Revision);
        Assert.Equal(Now + StoreRegistrySource.ViewTtl, Assert.Single(Assert.Single(later.Services).Instances).LastHeartbeatUtc);
    }

    // The overall status of a version, from its instances' states.
    [Theory]
    [InlineData(2, 0, 0, RegistryCapabilityStatus.Available)]
    [InlineData(1, 1, 0, RegistryCapabilityStatus.Degraded)]
    [InlineData(1, 0, 1, RegistryCapabilityStatus.Degraded)]
    [InlineData(0, 1, 1, RegistryCapabilityStatus.Unknown)]
    [InlineData(0, 2, 0, RegistryCapabilityStatus.Unavailable)]
    public async Task AVersionsStatus_FollowsItsInstances(int ready, int notReady, int unknown, RegistryCapabilityStatus expected)
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
    [InlineData(-1, RegistryInstanceState.Unknown)]
    [InlineData(0, RegistryInstanceState.NotReady)]
    public async Task AnInstanceNotHeardFromSinceTheWatchResumed_IsUnknown_ForOneLease(long ticksFromTheLease, RegistryInstanceState expected)
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        var resumed = Now - Defaults.Lease - TimeSpan.FromTicks(ticksFromTheLease);
        var silent = Instance(registration, resumed - TimeSpan.FromTicks(1));
        var reported = Instance(registration, Now);
        contents = new() { Registrations = [registration], Instances = [silent, reported], WatchResumedUtc = resumed };

        var states = Assert.Single((await CreateSut().GetStatusAsync(Cancel)).Services).Instances.ToDictionary(x => x.InstanceId, x => x.State);

        Assert.Equal(expected, states[silent.InstanceId]);
        Assert.Equal(RegistryInstanceState.Ready, states[reported.InstanceId]);
    }

    // An Unknown instance's revision is believed running: it takes part in the rolling rule.
    [Fact]
    public async Task AnUnknownInstance_TakesPartInTheRollingRule()
    {
        var old = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep", "CancelStep")], []));
        var rolled = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []), Start.AddMinutes(5));
        var resumed = Now - TimeSpan.FromSeconds(5);
        contents = new() { Registrations = [old, rolled], Instances = [Instance(old, Now), Instance(rolled, resumed - TimeSpan.FromMinutes(1))], WatchResumedUtc = resumed };

        var registry = await CreateSut().GetAsync(null, Cancel);

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

        var registry = await sut.GetAsync(null, Cancel);
        var capabilities = await sut.GetCapabilitiesAsync(Cancel);
        time.Advance(StoreRegistrySource.ViewTtl);
        contents = contents with { Instances = [Instance(registration, Now, healthy: false)] };
        var afterAFailure = await sut.GetCapabilitiesAsync(Cancel);

        Assert.Equal(RegistryCapabilityStatus.Available, Assert.Single(registry.Processes).Status);
        Assert.Equal(RegistryCapabilityStatus.Available, Assert.Single(registry.Queryables).Status);
        Assert.Equal(RegistryCapabilityStatus.NotReported, Assert.Single(capabilities.Processes).Status);
        Assert.Matches("^[0-9a-f]{64}$", capabilities.Revision!);
        Assert.Equal(capabilities.Revision, afterAFailure.Revision);
    }

    // The availability a host keeps fresh: each version's overall status, current version and address, no
    // instances. An instance joining within the same status changes nothing; a status flip does.
    [Fact]
    public async Task TheAvailability_ChangesOnlyWithAVersionsStatus()
    {
        var sut = CreateSut();
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        contents = new() { Registrations = [registration], Instances = [Instance(registration, Now, address: "https://radiology")], CurrentVersions = new Dictionary<string, string> { ["radiology"] = "1" } };

        var first = await sut.GetAvailabilityAsync(Cancel);
        time.Advance(StoreRegistrySource.ViewTtl);
        contents = contents with { Instances = [.. contents.Instances, Instance(registration, Now, address: "https://radiology")] };
        var anotherInstance = await sut.GetAvailabilityAsync(Cancel);
        time.Advance(StoreRegistrySource.ViewTtl);
        contents = contents with { Instances = [.. contents.Instances, Instance(registration, Now, healthy: false, address: "https://radiology")] };
        var degraded = await sut.GetAvailabilityAsync(Cancel);

        Assert.Equal(
            new RegistryServiceAvailability { ServiceName = "radiology", ServiceVersion = "1", IsCurrent = true, Address = "https://radiology", Status = RegistryCapabilityStatus.Available },
            Assert.Single(first.Services));
        Assert.Equal(first.Revision, anotherInstance.Revision);
        Assert.NotEqual(first.Revision, degraded.Revision);
        Assert.Equal(RegistryCapabilityStatus.Degraded, Assert.Single(degraded.Services).Status);
        Assert.Matches("^[0-9a-f]{64}$", first.Revision!);
    }

    // The registry sets the hosts' pace: availability at its heartbeat interval, capabilities at their own;
    // a change of pace is a new availability, so the hosts follow it.
    [Fact]
    public async Task TheAvailability_CarriesTheRegistrysPace()
    {
        var registration = Stored("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
        contents = new() { Registrations = [registration], Instances = [Instance(registration, Now)] };

        var standard = await CreateSut().GetAvailabilityAsync(Cancel);
        var slower = await CreateSut(Defaults with { HeartbeatInterval = TimeSpan.FromSeconds(20), CapabilitiesInterval = TimeSpan.FromMinutes(2) }).GetAvailabilityAsync(Cancel);

        Assert.Equal((Defaults.HeartbeatInterval, Defaults.CapabilitiesInterval), (standard.AvailabilityInterval, standard.CapabilitiesInterval));
        Assert.Equal((TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(2)), (slower.AvailabilityInterval, slower.CapabilitiesInterval));
        Assert.NotEqual(standard.Revision, slower.Revision);
    }
}

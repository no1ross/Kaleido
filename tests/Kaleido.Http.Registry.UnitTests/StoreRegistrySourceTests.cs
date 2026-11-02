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

    private RegistryRegistrationRecord Stored(string service, string version, RegistryCapabilities published, DateTimeOffset? registered = null)
    {
        var (record, json) = Registration(service, version, published, registered);
        capabilities[record.Revision] = json;
        return record;
    }

    private Task<RegistrySourceContents> ViewAsync(IEnumerable<RegistryRegistrationRecord> registrations, IEnumerable<RegistryInstanceRecord> instances, KaleidoRegistryOptions? options = null, params (string Service, string Version)[] current)
    {
        contents = new()
        {
            Registrations = [.. registrations],
            Instances = [.. instances],
            CurrentVersions = current.ToDictionary(x => x.Service, x => x.Version, StringComparer.OrdinalIgnoreCase),
        };

        return CreateSut(options).GetAsync(maxParallelism: null, Cancel);
    }

    [Fact]
    public async Task AReadyInstance_PublishesItsVersionsCapabilities_AndItsStatus()
    {
        var registration = Stored("radiology", "2026-11", new([ProcessorWith("radiology", "OrderScanStep")], [SourceWith("radiology", "ScanSource")]));

        var view = await ViewAsync([registration], [Instance(registration, Now, address: "https://radiology")], current: ("radiology", "2026-11"));

        Assert.Equal(["OrderScanStep"], Assert.Single(view.Processes).Steps!.Select(x => x.Name));
        Assert.Equal("ScanSource", Assert.Single(view.Queryables).Name);
        Assert.Equal(
            new RegistryServiceStatus { ServiceName = "radiology", ServiceVersion = "2026-11", IsCurrent = true, Address = "https://radiology", ReadyInstances = 1, Instances = 1, Available = true },
            Assert.Single(view.Services));
        Assert.Empty(view.Missing);
        Assert.Empty(view.Errors);
    }

    // The lease: ready while the last heartbeat is within MissedHeartbeats x HeartbeatInterval.
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public async Task AnInstance_IsReadyAroundTheEndOfItsLease(long ticksPastTheLease, int ready)
    {
        var registration = Stored("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []));
        var heartbeat = Now - Defaults.Lease - TimeSpan.FromTicks(ticksPastTheLease);

        var status = Assert.Single((await ViewAsync([registration], [Instance(registration, heartbeat)])).Services);

        Assert.Equal(ready, status.ReadyInstances);
        Assert.Equal(ready == 1, status.Available);
        Assert.Equal(1, status.Instances);
    }

    // A version with no ready instance stays listed, flagged unavailable.
    [Fact]
    public async Task AVersionWithNoReadyInstance_KeepsItsCapabilities_Unavailable()
    {
        var registration = Stored("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []));

        var view = await ViewAsync([registration], [Instance(registration, Now, healthy: false)]);

        Assert.Single(view.Processes);
        Assert.False(Assert.Single(view.Services).Available);
    }

    [Fact]
    public async Task RegistrationsNoInstanceRuns_StayListed_Unavailable()
    {
        var registration = Stored("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []));

        var view = await ViewAsync([registration], []);

        Assert.Single(view.Processes);
        Assert.Equal(0, Assert.Single(view.Services).Instances);
    }

    // The rolling-deployment rule: only what every ready instance's revision has, as the oldest defines it.
    [Fact]
    public async Task DuringARollingDeployment_OnlyTheSharedCapabilities_ArePublished_AsTheOldestRevisionDefinesThem()
    {
        var old = Stored("radiology", "1", new(
            [ProcessorWith("radiology", "OrderScanStep", "CancelStep") with { Description = "old" }],
            [SourceWith("radiology", "ScanSource", "ScanCard", "OldView"), SourceWith("radiology", "Retired")]));
        var rolled = Stored("radiology", "1", new(
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
        var old = Stored("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep", "CancelStep")], []));
        var failed = Stored("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []), Start.AddMinutes(5));

        var view = await ViewAsync([old, failed], [Instance(old, Now), Instance(failed, Now, healthy: false)]);

        Assert.Equal(["CancelStep", "OrderScanStep"], Assert.Single(view.Processes).Steps!.Select(x => x.Name).Order(StringComparer.Ordinal));
    }

    // Versions are kept apart; only the pointer says which is current.
    [Fact]
    public async Task TwoVersions_AreEachListed_AndOnlyTheCurrentOneIsMarked()
    {
        var v1 = Stored("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []));
        var v2 = Stored("radiology", "2", new([ProcessorWith("radiology", "OrderScanStep", "ReviewStep")], []));

        var view = await ViewAsync([v1, v2], [Instance(v1, Now), Instance(v2, Now)], current: ("radiology", "1"));

        Assert.Equal(2, view.Processes.Count);
        Assert.Equal([("1", true), ("2", false)], view.Services.Select(x => (x.ServiceVersion, x.IsCurrent)));
    }

    // The address the instance seen most recently registered.
    [Fact]
    public async Task TheAddress_IsTheOneTheLatestInstanceRegistered()
    {
        var registration = Stored("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []));

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
        var registration = Stored("radiology", "1", new([ProcessorWith("radiology") with { Steps = [step] }], []));

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
        var v1 = Stored("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []));
        var v2 = Stored("radiology", "2", new([ProcessorWith("radiology", "ReviewStep")], []));
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

    // The statuses, apart: their revision follows their content only.
    [Fact]
    public async Task TheStatus_HasItsOwnRevision_FollowingTheStatusesOnly()
    {
        var sut = CreateSut();
        var registration = Stored("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []));
        var instance = Instance(registration, Now);
        contents = new() { Registrations = [registration], Instances = [instance] };

        var first = await sut.GetStatusAsync(Cancel);
        time.Advance(StoreRegistrySource.ViewTtl);
        var same = await sut.GetStatusAsync(Cancel);
        time.Advance(StoreRegistrySource.ViewTtl);
        contents = contents with { Instances = [instance, Instance(registration, Now)] };
        var changed = await sut.GetStatusAsync(Cancel);

        Assert.Equal((1, time.GetUtcNow() - (StoreRegistrySource.ViewTtl * 2)), (Assert.Single(first.Services).Instances, first.GeneratedAt));
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
}

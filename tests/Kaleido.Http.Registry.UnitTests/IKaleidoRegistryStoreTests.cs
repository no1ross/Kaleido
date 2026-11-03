using System.Globalization;
using static Kaleido.Http.Registry.UnitTests.RegistryTestData;

namespace Kaleido.Http.Registry.UnitTests;

// The store contract every registry store passes (#553): the in-memory store here, the SQLite store in
// its provider's tests, a host's own store in its tests. Each test gets a fresh database; every
// CreateStore() call is another connection to it (another scope, as each registry request has).
public abstract class IKaleidoRegistryStoreTests
    : SutFixture
{
    private static readonly DateTimeOffset At = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly (RegistryRegistrationRecord Record, string Capabilities) V1 = Registration("radiology", "1", Entry([ProcessorWith("radiology", "OrderScanStep")], []));
    private static readonly (RegistryRegistrationRecord Record, string Capabilities) V2 = Registration("radiology", "2", Entry([ProcessorWith("radiology", "ReviewStep")], []));

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    // A store on this test's database.
    protected abstract IKaleidoRegistryStore CreateStore();

    // Insert-if-absent: many instances of one build send the same registration.
    [Fact]
    public async Task ARegistration_IsStoredOnce_PerRevision_WithItsCapabilities()
    {
        var store = CreateStore();

        await store.AddRegistrationAsync(V1.Record, V1.Capabilities, Cancel);
        await store.AddRegistrationAsync(V1.Record with { RegisteredUtc = At.AddHours(1) }, "other", Cancel);
        await store.AddRegistrationAsync(V2.Record, V2.Capabilities, Cancel);

        Assert.Equal([V1.Record, V2.Record], (await CreateStore().ReadAsync(Cancel)).Registrations.OrderBy(x => x.ServiceVersion, StringComparer.Ordinal));
        Assert.Equal(
            new Dictionary<string, string> { [V1.Record.Revision] = V1.Capabilities },
            await CreateStore().GetCapabilitiesAsync([V1.Record.Revision, "gone"], Cancel));
        Assert.Empty(await CreateStore().GetCapabilitiesAsync([], Cancel));
    }

    [Fact]
    public async Task AnInstance_IsSaved_AndReplaced()
    {
        var instance = Instance(V1.Record, At, address: "https://old");

        await CreateStore().SaveInstanceAsync(instance, Cancel);
        await CreateStore().SaveInstanceAsync(instance with { Address = "https://new", Healthy = false, LastHeartbeatUtc = At.AddSeconds(5) }, Cancel);

        Assert.Equal([instance with { Address = "https://new", Healthy = false, LastHeartbeatUtc = At.AddSeconds(5) }], (await CreateStore().ReadAsync(Cancel)).Instances);
    }

    // A renewal and a removal check the instance's service against what the caller may change.
    [Fact]
    public async Task RenewAndRemove_AreConditional_OnTheInstancesService()
    {
        var instance = Instance(V1.Record, At);
        await CreateStore().SaveInstanceAsync(instance, Cancel);

        Assert.Equal(RegistryInstanceChange.Unknown, await CreateStore().RenewInstanceAsync(Guid.NewGuid(), At, healthy: true, null, Cancel));
        Assert.Equal(RegistryInstanceChange.NotAllowed, await CreateStore().RenewInstanceAsync(instance.InstanceId, At.AddHours(1), healthy: false, ["member"], Cancel));
        Assert.Equal(RegistryInstanceChange.NotAllowed, await CreateStore().RemoveInstanceAsync(instance.InstanceId, [], Cancel));
        Assert.Equal([instance], (await CreateStore().ReadAsync(Cancel)).Instances);

        Assert.Equal(RegistryInstanceChange.Done, await CreateStore().RenewInstanceAsync(instance.InstanceId, At.AddSeconds(10), healthy: false, ["RADIOLOGY"], Cancel));
        Assert.Equal([instance with { LastHeartbeatUtc = At.AddSeconds(10), Healthy = false }], (await CreateStore().ReadAsync(Cancel)).Instances);

        Assert.Equal(RegistryInstanceChange.Done, await CreateStore().RemoveInstanceAsync(instance.InstanceId, null, Cancel));
        Assert.Equal(RegistryInstanceChange.Unknown, await CreateStore().RemoveInstanceAsync(instance.InstanceId, null, Cancel));
        Assert.Empty((await CreateStore().ReadAsync(Cancel)).Instances);
    }

    // The retention: an instance last seen before the cutoff goes, with the registrations no instance runs.
    [Fact]
    public async Task RemoveInstancesSeenBefore_RemovesTheStale_AndTheRegistrationsNoOneRuns()
    {
        var store = CreateStore();
        var cutoff = At.AddHours(1);
        var stale = Instance(V1.Record, cutoff.AddTicks(-1));
        var atTheCutoff = Instance(V2.Record, cutoff);
        await store.AddRegistrationAsync(V1.Record, V1.Capabilities, Cancel);
        await store.AddRegistrationAsync(V2.Record, V2.Capabilities, Cancel);
        await store.SaveInstanceAsync(stale, Cancel);
        await store.SaveInstanceAsync(atTheCutoff, Cancel);

        var removed = await CreateStore().RemoveInstancesSeenBeforeAsync(cutoff, Cancel);

        var contents = await CreateStore().ReadAsync(Cancel);
        Assert.Equal(1, removed);
        Assert.Equal([atTheCutoff], contents.Instances);
        Assert.Equal([V2.Record], contents.Registrations);
        Assert.Equal(0, await CreateStore().RemoveInstancesSeenBeforeAsync(cutoff, Cancel));
    }

    // The first version registered is current; a later one never takes over here.
    [Fact]
    public async Task TheCurrentVersion_IsTheFirstOneSet()
    {
        Assert.Equal("1", await CreateStore().GetOrSetCurrentVersionAsync("radiology", "1", Cancel));
        Assert.Equal("1", await CreateStore().GetOrSetCurrentVersionAsync("RADIOLOGY", "2", Cancel));
        Assert.Equal("1", (await CreateStore().ReadAsync(Cancel)).CurrentVersions["Radiology"]);
    }

    // Every write is atomic: concurrent writers of one registration, one instance and one pointer agree,
    // and concurrent sweeps remove each instance once.
    [Fact]
    public async Task ConcurrentWrites_AreEachAtomic()
    {
        var instance = Instance(V1.Record, At);
        await CreateStore().SaveInstanceAsync(instance, Cancel);
        var stale = Enumerable.Range(0, 10).Select(i => Instance(V2.Record, At.AddDays(-2))).ToList();

        foreach (var old in stale)
        {
            await CreateStore().SaveInstanceAsync(old, Cancel);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(
            async () =>
            {
                var store = CreateStore();
                await store.AddRegistrationAsync(V1.Record, V1.Capabilities, Cancel);
                var renewed = await store.RenewInstanceAsync(instance.InstanceId, At.AddSeconds(i), healthy: true, null, Cancel);
                var current = await store.GetOrSetCurrentVersionAsync("radiology", i.ToString(CultureInfo.InvariantCulture), Cancel);
                var swept = await store.RemoveInstancesSeenBeforeAsync(At.AddDays(-1), Cancel);
                return (renewed, current, swept);
            },
            Cancel)));

        var contents = await CreateStore().ReadAsync(Cancel);
        Assert.Equal([V1.Record], contents.Registrations);
        Assert.All(results, x => Assert.Equal(RegistryInstanceChange.Done, x.renewed));
        Assert.Single(results.Select(x => x.current).Distinct(StringComparer.Ordinal));
        Assert.Equal(results[0].current, contents.CurrentVersions["radiology"]);
        Assert.Equal(stale.Count, results.Sum(x => x.swept));
        Assert.Equal([instance.InstanceId], contents.Instances.Select(x => x.InstanceId));
    }
}

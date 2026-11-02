using Microsoft.Extensions.Logging;
using static Kaleido.Http.Registry.UnitTests.RegistryTestData;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class InMemoryKaleidoRegistryStoreTests
    : SutFixture
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static InMemoryKaleidoRegistryStore CreateSut(ILogger<InMemoryKaleidoRegistryStore>? logger = null) =>
        new(logger ?? Mock.Of<ILogger<InMemoryKaleidoRegistryStore>>());

    private static readonly (RegistryRegistrationRecord Record, string Capabilities) V1 = Registration("radiology", "1", new([ProcessorWith("radiology", "OrderScanStep")], []));
    private static readonly (RegistryRegistrationRecord Record, string Capabilities) V2 = Registration("radiology", "2", new([ProcessorWith("radiology", "ReviewStep")], []));

    // Insert-if-absent: many instances of one build send the same registration.
    [Fact]
    public async Task ARegistration_IsStoredOnce_PerRevision_WithItsCapabilities()
    {
        var sut = CreateSut();

        await sut.AddRegistrationAsync(V1.Record, V1.Capabilities, Cancel);
        await sut.AddRegistrationAsync(V1.Record with { RegisteredUtc = Start.AddHours(1) }, "other", Cancel);
        await sut.AddRegistrationAsync(V2.Record, V2.Capabilities, Cancel);

        Assert.Equal([V1.Record, V2.Record], (await sut.ReadAsync(Cancel)).Registrations);
        Assert.Equal(
            new Dictionary<string, string> { [V1.Record.Revision] = V1.Capabilities },
            await sut.GetCapabilitiesAsync([V1.Record.Revision, V1.Record.Revision, "gone"], Cancel));
    }

    [Fact]
    public async Task AnInstance_IsSaved_AndReplaced()
    {
        var sut = CreateSut();
        var instance = Instance(V1.Record, Start);

        await sut.SaveInstanceAsync(instance, Cancel);
        await sut.SaveInstanceAsync(instance with { Address = "https://new" }, Cancel);

        Assert.Equal([instance with { Address = "https://new" }], (await sut.ReadAsync(Cancel)).Instances);
    }

    // A renewal and a removal check the instance's service against what the caller may change.
    [Fact]
    public async Task RenewAndRemove_AreConditional_OnTheInstancesService()
    {
        var sut = CreateSut();
        var instance = Instance(V1.Record, Start);
        await sut.SaveInstanceAsync(instance, Cancel);

        Assert.Equal(RegistryInstanceChange.Unknown, await sut.RenewInstanceAsync(Guid.NewGuid(), Start, healthy: true, null, Cancel));
        Assert.Equal(RegistryInstanceChange.NotAllowed, await sut.RenewInstanceAsync(instance.InstanceId, Start.AddHours(1), healthy: false, ["member"], Cancel));
        Assert.Equal(RegistryInstanceChange.NotAllowed, await sut.RemoveInstanceAsync(instance.InstanceId, [], Cancel));
        Assert.Equal([instance], (await sut.ReadAsync(Cancel)).Instances);

        Assert.Equal(RegistryInstanceChange.Done, await sut.RenewInstanceAsync(instance.InstanceId, Start.AddSeconds(10), healthy: false, ["RADIOLOGY"], Cancel));
        Assert.Equal([instance with { LastHeartbeatUtc = Start.AddSeconds(10), Healthy = false }], (await sut.ReadAsync(Cancel)).Instances);

        Assert.Equal(RegistryInstanceChange.Done, await sut.RemoveInstanceAsync(instance.InstanceId, null, Cancel));
        Assert.Equal(RegistryInstanceChange.Unknown, await sut.RemoveInstanceAsync(instance.InstanceId, null, Cancel));
        Assert.Empty((await sut.ReadAsync(Cancel)).Instances);
    }

    // The retention: an instance last seen before the cutoff goes, with the registrations no instance runs.
    [Fact]
    public async Task RemoveInstancesSeenBefore_RemovesTheStale_AndTheRegistrationsNoOneRuns()
    {
        var sut = CreateSut();
        var cutoff = Start.AddHours(1);
        var stale = Instance(V1.Record, cutoff.AddTicks(-1));
        var atTheCutoff = Instance(V2.Record, cutoff);
        await sut.AddRegistrationAsync(V1.Record, V1.Capabilities, Cancel);
        await sut.AddRegistrationAsync(V2.Record, V2.Capabilities, Cancel);
        await sut.SaveInstanceAsync(stale, Cancel);
        await sut.SaveInstanceAsync(atTheCutoff, Cancel);

        var removed = await sut.RemoveInstancesSeenBeforeAsync(cutoff, Cancel);

        var contents = await sut.ReadAsync(Cancel);
        Assert.Equal(1, removed);
        Assert.Equal([atTheCutoff], contents.Instances);
        Assert.Equal([V2.Record], contents.Registrations);
        Assert.Equal(0, await sut.RemoveInstancesSeenBeforeAsync(cutoff, Cancel));
    }

    // The first version registered is current; a later one never takes over here.
    [Fact]
    public async Task TheCurrentVersion_IsTheFirstOneSet()
    {
        var sut = CreateSut();

        Assert.Equal("1", await sut.GetOrSetCurrentVersionAsync("radiology", "1", Cancel));
        Assert.Equal("1", await sut.GetOrSetCurrentVersionAsync("RADIOLOGY", "2", Cancel));
        Assert.Equal("1", (await sut.ReadAsync(Cancel)).CurrentVersions["radiology"]);
    }

    // Every write is atomic: concurrent writers of one registration, one instance and one pointer agree.
    [Fact]
    public async Task ConcurrentWrites_AreEachAtomic()
    {
        var sut = CreateSut();
        var instance = Instance(V1.Record, Start);
        await sut.SaveInstanceAsync(instance, Cancel);

        var versions = await Task.WhenAll(Enumerable.Range(0, 50).Select(async i =>
        {
            await sut.AddRegistrationAsync(V1.Record, V1.Capabilities, Cancel);
            await sut.RenewInstanceAsync(instance.InstanceId, Start.AddSeconds(i), healthy: true, null, Cancel);
            return await sut.GetOrSetCurrentVersionAsync("radiology", i.ToString(System.Globalization.CultureInfo.InvariantCulture), Cancel);
        }));

        var contents = await sut.ReadAsync(Cancel);
        Assert.Single(contents.Registrations);
        Assert.Single(versions.Distinct(StringComparer.Ordinal));
        Assert.Equal(versions[0], contents.CurrentVersions["radiology"]);
    }

    // A read is a copy: later writes do not change it.
    [Fact]
    public async Task Read_ReturnsACopy()
    {
        var sut = CreateSut();
        var read = await sut.ReadAsync(Cancel);

        await sut.AddRegistrationAsync(V1.Record, V1.Capabilities, Cancel);
        await sut.SaveInstanceAsync(Instance(V1.Record, Start), Cancel);
        await sut.GetOrSetCurrentVersionAsync("radiology", "1", Cancel);

        Assert.Empty(read.Registrations);
        Assert.Empty(read.Instances);
        Assert.Empty(read.CurrentVersions);
    }

    [Fact]
    public void Created_WarnsThatNothingSurvivesARestart()
    {
        var logger = new Mock<ILogger<InMemoryKaleidoRegistryStore>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _ = CreateSut(logger.Object);

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log)).Arguments[0]);
    }
}

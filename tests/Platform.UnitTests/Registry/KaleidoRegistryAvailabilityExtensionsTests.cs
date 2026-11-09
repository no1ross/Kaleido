using Kaleido.Platform.Processor;
using Kaleido.Platform.Queryable;

namespace Kaleido.Platform.Registry.UnitTests;

// #553: the registry a consumer reads - each capability with its service version's overall status, from the availability.
public sealed class KaleidoRegistryAvailabilityExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly KaleidoRegistry Registry = new()
    {
        Processes =
        [
            new ProcessorRegistryResponse { Name = "radiology", ServiceName = "radiology", ServiceVersion = "1" },
            new ProcessorRegistryResponse { Name = "radiology", ServiceName = "radiology", ServiceVersion = "2" },
        ],
        Queryables = [new QueryableSourceResponse { Name = "MemberSource", ServiceName = "member", ServiceVersion = "1" }],
    };

    private static RegistryServiceAvailability Status(string service, string version, RegistryCapabilityStatus status) =>
        new() { ServiceName = service, ServiceVersion = version, Status = status };

    [Fact]
    public void WithAvailability_StampsEachCapability_WithItsVersionsStatus()
    {
        var status = new KaleidoRegistryAvailability
        {
            Services = [Status("Radiology", "1", RegistryCapabilityStatus.Degraded), Status("radiology", "2", RegistryCapabilityStatus.Available), Status("member", "1", RegistryCapabilityStatus.Unknown)],
        };

        var registry = Registry.WithAvailability(status);

        Assert.Equal([RegistryCapabilityStatus.Degraded, RegistryCapabilityStatus.Available], registry.Processes.Select(x => x.Status));
        Assert.Equal(RegistryCapabilityStatus.Unknown, Assert.Single(registry.Queryables).Status);
    }

    // A capability whose version no status lists has no instance anyone knows of.
    [Fact]
    public void WithAvailability_AVersionNotListed_IsUnavailable()
    {
        var registry = Registry.WithAvailability(new KaleidoRegistryAvailability { Services = [Status("radiology", "1", RegistryCapabilityStatus.Available)] });

        Assert.Equal(RegistryCapabilityStatus.Unavailable, registry.Processes.ElementAt(1).Status);
        Assert.Equal(RegistryCapabilityStatus.Unavailable, Assert.Single(registry.Queryables).Status);
    }

    // No statuses at all (a host that reads no control registry): nothing is reported.
    [Fact]
    public void WithAvailability_WithoutAnyStatus_LeavesTheCapabilitiesAsTheyAre()
    {
        var registry = Registry.WithAvailability(new KaleidoRegistryAvailability());

        Assert.Equal(Registry.Processes, registry.Processes);
        Assert.All(registry.Processes, x => Assert.Equal(RegistryCapabilityStatus.NotReported, x.Status));
    }

    // The composed revision follows both parts: the host rebuilds exactly when either changes.
    [Fact]
    public void WithAvailability_TheRevision_FollowsTheCapabilitiesAndTheAvailability()
    {
        var capabilities = Registry with { Revision = "caps-1" };
        var availability = new KaleidoRegistryAvailability { Services = [Status("radiology", "1", RegistryCapabilityStatus.Available)], Revision = "avail-1", GeneratedAt = DateTimeOffset.UnixEpoch.AddDays(1) };

        var composed = capabilities.WithAvailability(availability);

        Assert.Matches("^[0-9a-f]{64}$", composed.Revision!);
        Assert.Equal(composed.Revision, capabilities.WithAvailability(availability).Revision);
        Assert.NotEqual(composed.Revision, (capabilities with { Revision = "caps-2" }).WithAvailability(availability).Revision);
        Assert.NotEqual(composed.Revision, capabilities.WithAvailability(availability with { Revision = "avail-2" }).Revision);
        Assert.Null(Registry.WithAvailability(availability).Revision);
        Assert.Equal(availability.GeneratedAt, composed.GeneratedAt);
    }

    [Fact]
    public void WithAvailability_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ((KaleidoRegistry)null!).WithAvailability(new KaleidoRegistryAvailability()));
        Assert.Throws<ArgumentNullException>(() => Registry.WithAvailability(null!));
    }
}

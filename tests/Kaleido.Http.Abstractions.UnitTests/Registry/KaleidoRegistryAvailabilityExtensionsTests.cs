using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.Registry.UnitTests;

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
    public void WithAvailability_WithoutAnyStatus_LeavesTheRegistryAsItIs()
    {
        Assert.Same(Registry, Registry.WithAvailability(new KaleidoRegistryAvailability()));
    }

    [Fact]
    public void WithAvailability_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ((KaleidoRegistry)null!).WithAvailability(new KaleidoRegistryAvailability()));
        Assert.Throws<ArgumentNullException>(() => Registry.WithAvailability(null!));
    }
}

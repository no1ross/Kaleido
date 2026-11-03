using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;

namespace Kaleido.Http.Registry.UnitTests;

// #553: the registry a consumer reads - each capability with its service version's overall status.
public sealed class KaleidoRegistryStatusExtensionsTests
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

    private static RegistryServiceStatus Status(string service, string version, RegistryCapabilityStatus status) =>
        new() { ServiceName = service, ServiceVersion = version, Status = status };

    [Fact]
    public void WithStatuses_StampsEachCapability_WithItsVersionsStatus()
    {
        var status = new KaleidoRegistryStatus
        {
            Services = [Status("Radiology", "1", RegistryCapabilityStatus.Degraded), Status("radiology", "2", RegistryCapabilityStatus.Available), Status("member", "1", RegistryCapabilityStatus.Unknown)],
        };

        var registry = Registry.WithStatuses(status);

        Assert.Equal([RegistryCapabilityStatus.Degraded, RegistryCapabilityStatus.Available], registry.Processes.Select(x => x.Status));
        Assert.Equal(RegistryCapabilityStatus.Unknown, Assert.Single(registry.Queryables).Status);
    }

    // A capability whose version no status lists has no instance anyone knows of.
    [Fact]
    public void WithStatuses_AVersionNotListed_IsUnavailable()
    {
        var registry = Registry.WithStatuses(new KaleidoRegistryStatus { Services = [Status("radiology", "1", RegistryCapabilityStatus.Available)] });

        Assert.Equal(RegistryCapabilityStatus.Unavailable, registry.Processes.ElementAt(1).Status);
        Assert.Equal(RegistryCapabilityStatus.Unavailable, Assert.Single(registry.Queryables).Status);
    }

    // No statuses at all (a host that reads no control registry): nothing is reported.
    [Fact]
    public void WithStatuses_WithoutAnyStatus_LeavesTheRegistryAsItIs()
    {
        Assert.Same(Registry, Registry.WithStatuses(new KaleidoRegistryStatus()));
    }

    [Fact]
    public void WithStatuses_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ((KaleidoRegistry)null!).WithStatuses(new KaleidoRegistryStatus()));
        Assert.Throws<ArgumentNullException>(() => Registry.WithStatuses(null!));
    }
}

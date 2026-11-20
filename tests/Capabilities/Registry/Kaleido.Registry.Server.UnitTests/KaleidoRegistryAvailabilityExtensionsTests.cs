namespace Kaleido.Registry.Server.UnitTests;

public sealed class KaleidoRegistryAvailabilityExtensionsTests : SutFixture
{
    private static readonly KaleidoRegistry Registry = new()
    {
        Processes =
        [
            RegistryTestData.ProcessorWith("radiology", "inspect") with { ServiceVersion = "1" },
            RegistryTestData.ProcessorWith("radiology", "inspect") with { ServiceVersion = "2" },
        ],
        Queryables = [RegistryTestData.SourceWith("member", "MemberSource") with { ServiceVersion = "1" }],
    };

    private static RegistryServiceAvailability Status(string service, string version, string status) =>
        new() { ServiceName = service, ServiceVersion = version, Status = status };

    [Fact]
    public void WithAvailability_StampsEachCapability_WithItsVersionsStatus()
    {
        var availability = new KaleidoRegistryAvailability
        {
            Services =
            [
                Status("Radiology", "1", RegistryStatusNames.Degraded),
                Status("radiology", "2", RegistryStatusNames.Available),
                Status("member", "1", RegistryStatusNames.Unknown),
            ],
        };

        var registry = Registry.WithAvailability(availability);

        Assert.Equal([RegistryStatusNames.Degraded, RegistryStatusNames.Available], registry.Processes.Select(x => x.Status));
        Assert.Equal(RegistryStatusNames.Unknown, Assert.Single(registry.Queryables).Status);
    }

    [Fact]
    public void WithAvailability_AVersionNotListed_IsUnavailable()
    {
        var registry = Registry.WithAvailability(new KaleidoRegistryAvailability
        {
            Services = [Status("radiology", "1", RegistryStatusNames.Available)],
        });

        Assert.Equal(RegistryStatusNames.Unavailable, registry.Processes.ElementAt(1).Status);
        Assert.Equal(RegistryStatusNames.Unavailable, Assert.Single(registry.Queryables).Status);
    }

    [Fact]
    public void WithAvailability_WithoutAnyStatus_LeavesTheCapabilitiesAsTheyAre()
    {
        var generatedAt = RegistryTestData.Start;
        var registry = Registry.WithAvailability(new KaleidoRegistryAvailability { GeneratedAt = generatedAt });

        Assert.Same(Registry.Processes, registry.Processes);
        Assert.All(registry.Processes, x => Assert.Equal(RegistryStatusNames.NotReported, x.Status));
        Assert.Equal(generatedAt, registry.GeneratedAt);
    }

    [Fact]
    public void WithAvailability_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ((KaleidoRegistry)null!).WithAvailability(new KaleidoRegistryAvailability()));
        Assert.Throws<ArgumentNullException>(() => Registry.WithAvailability(null!));
    }
}

using Kaleido.Platform.Registry;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Registry.UnitTests;

public sealed class RegistryCompletenessHealthCheckTests
    : SutFixture
{
    private readonly Mock<IKaleidoRegistrySource> registrations = new();
    private readonly Mock<IRegistryStatusReader> statuses = new();

    private RegistryCompletenessHealthCheck CreateSut() => new(registrations.Object, statuses.Object);

    private static RegistryServiceStatus Status(string version, RegistryCapabilityStatus status, int unknown = 0) =>
        new() { ServiceName = "radiology", ServiceVersion = version, Status = status, UnknownInstances = unknown };

    private void View(RegistryServiceStatus[] services, RegistryMissingService[] missing)
    {
        registrations.Setup(x => x.GetAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new KaleidoRegistry { Missing = missing });
        statuses.Setup(x => x.GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KaleidoRegistryStatus { Services = services });
    }

    private Task<HealthCheckResult> CheckAsync() => CreateSut().CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

    // A Degraded version still serves: only missing, unavailable or unconfirmed degrade the registry.
    [Theory]
    [InlineData(RegistryCapabilityStatus.Available)]
    [InlineData(RegistryCapabilityStatus.Degraded)]
    public async Task EverythingRegisteredAndServing_IsHealthy(RegistryCapabilityStatus status)
    {
        View([Status("1", status)], []);

        Assert.Equal(HealthStatus.Healthy, (await CheckAsync()).Status);
    }

    [Fact]
    public async Task SomethingMissingOrUnavailable_IsDegraded_NamingIt()
    {
        View(
            [Status("1", RegistryCapabilityStatus.Unavailable), Status("2", RegistryCapabilityStatus.Available)],
            [new RegistryMissingService { ServiceName = "member", Reason = "lookup" }, new RegistryMissingService { ServiceName = "clinic", ServiceVersion = "3", Reason = "expected" }]);

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Missing: member, clinic/3; no ready instance: radiology/1; waiting for 0 instance(s) to report since the registry resumed (unknown: none).", result.Description);
        Assert.Equal(["radiology/1"], (IEnumerable<string>)result.Data["unavailable"]);
    }

    [Fact]
    public async Task OnlyAnUnavailableVersion_IsDegraded()
    {
        View([Status("1", RegistryCapabilityStatus.Unavailable)], []);

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.StartsWith("Missing: none;", result.Description, StringComparison.Ordinal);
    }

    // After every registry node was down: Degraded until every instance has reported, even in a version
    // that already serves.
    [Fact]
    public async Task InstancesNotYetHeardFrom_AreDegraded_NamingTheUnknownVersions()
    {
        View([Status("1", RegistryCapabilityStatus.Unknown, unknown: 2), Status("2", RegistryCapabilityStatus.Degraded, unknown: 1)], []);

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.EndsWith("waiting for 3 instance(s) to report since the registry resumed (unknown: radiology/1).", result.Description, StringComparison.Ordinal);
        Assert.Equal(3, result.Data["unconfirmedInstances"]);
    }
}

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class RegistryCompletenessHealthCheckTests
    : SutFixture
{
    private readonly Mock<IKaleidoRegistrySource> registrations = new();

    private RegistryCompletenessHealthCheck CreateSut() => new(registrations.Object);

    private static RegistryServiceStatus Status(string version, bool available) =>
        new() { ServiceName = "radiology", ServiceVersion = version, Available = available };

    private void View(RegistryServiceStatus[] services, RegistryMissingService[] missing) =>
        registrations.Setup(x => x.GetAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistrySourceContents([], [], []) { Services = services, Missing = missing });

    private Task<HealthCheckResult> CheckAsync() => CreateSut().CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task EverythingRegisteredAndReady_IsHealthy()
    {
        View([Status("1", available: true)], []);

        Assert.Equal(HealthStatus.Healthy, (await CheckAsync()).Status);
    }

    [Fact]
    public async Task SomethingMissingOrUnavailable_IsDegraded_NamingIt()
    {
        View(
            [Status("1", available: false), Status("2", available: true)],
            [new RegistryMissingService { ServiceName = "member", Reason = "lookup" }, new RegistryMissingService { ServiceName = "clinic", ServiceVersion = "3", Reason = "expected" }]);

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Missing: member, clinic/3; no ready instance: radiology/1.", result.Description);
        Assert.Equal(["radiology/1"], (IEnumerable<string>)result.Data["unavailable"]);
    }

    [Fact]
    public async Task OnlyAnUnavailableVersion_IsDegraded()
    {
        View([Status("1", available: false)], []);

        var result = await CheckAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.StartsWith("Missing: none;", result.Description, StringComparison.Ordinal);
    }
}
